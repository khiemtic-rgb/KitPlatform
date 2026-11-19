# -*- coding: utf-8 -*-
"""Sinh migrations/447_kap_pharmacy_v2.sql từ build.py.

Chạy: python3 gen_sql.py  (tự gọi build.py để kiểm tra trước)
"""
import json, sys
import build as B

V1_TEMPLATE_ID = "a0000001-0000-4000-8000-000000000001"


def q(s):
    return "NULL" if s is None else "'" + str(s).replace("'", "''") + "'"


def j(obj):
    return q(json.dumps(obj, ensure_ascii=False)) + "::jsonb"


def money(v):
    """Làm tròn kiểu dễ đọc: dưới 20 triệu -> tròn triệu; trên -> tròn 5 triệu."""
    m = v / 1_000_000
    m = round(m) if m < 20 else int(5 * round(m / 5))
    return f"{max(m, 1)} triệu đồng"


def rec_rank(impact):
    return 1 if impact >= 1.2 else 2 if impact >= 1.0 else 3 if impact >= 0.9 else 4


def main():
    t, ins = B.build()
    errs, allq, _ = B.validate(t, ins)
    if errs:
        print("\n".join(errs)); sys.exit(1)

    out = []
    w = out.append
    w("-- KitPlatform 447: KAP PHARMACY_V2 v2.0 — 'Soát nhanh nhà thuốc'")
    w("-- Sinh tự động từ tools/kap-v2/build.py + gen_sql.py (chạy trong thư mục đó). Không sửa tay; sửa build.py rồi sinh lại.")
    w("-- Depends on: 068, 089. PHARMACY_V1 giữ nguyên (active) để bài nháp cũ làm tiếp được.")
    w("-- Không seed benchmark cohort cho V2: chỉ so sánh khi đã có dữ liệu thật (xem AssessmentSettings).")
    w("")
    w("BEGIN;")
    w("")
    tid = t["id"]
    w("INSERT INTO assessment_template (id, code, name, version, description, verticals, status)")
    w(f"VALUES ({q(tid)}, {q(t['code'])}, {q(t['name'])}, {q(t['version'])},")
    w(f"        {q('31 câu hiển thị (36 câu trong ngân hàng, rẽ nhánh theo nỗi đau ở P3). 24 câu tính điểm, 6 nhóm.')},")
    w("        ARRAY['pharmacy','pharmacy_chain'], 'active')")
    w("ON CONFLICT (code, version) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description,")
    w("    status = EXCLUDED.status, updated_at = NOW();")
    w("")

    for c in t["categories"]:
        cid = B.uid("cat", c["code"])
        w(f"-- {c['name']} ({c['code']})")
        w("INSERT INTO assessment_category (id, template_id, code, name, sort_order, weight)")
        w(f"VALUES ({q(cid)}, {q(tid)}, {q(c['code'])}, {q(c['name'])}, {c['sortOrder']}, {c['weight']})")
        w("ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, sort_order = EXCLUDED.sort_order, weight = EXCLUDED.weight;")
        for d in c["dimensions"]:
            did = B.uid("dim", d["code"])
            w("INSERT INTO assessment_dimension (id, category_id, code, name, sort_order, weight)")
            w(f"VALUES ({q(did)}, {q(cid)}, {q(d['code'])}, {q(d['name'])}, 1, 1)")
            w("ON CONFLICT (category_id, code) DO UPDATE SET name = EXCLUDED.name;")
            for qu in d["questions"]:
                w("INSERT INTO assessment_question (id, dimension_id, code, title, help_text, question_type, scorable, required, sort_order, weight, metadata)")
                w(f"VALUES ({q(qu['id'])}, {q(did)}, {q(qu['code'])}, {q(qu['title'])}, {q(qu['helpText'])},")
                w(f"        'single_choice', {str(qu['scorable']).upper()}, TRUE, {qu['sortOrder']}, 1, {j(qu['metadata'] or {})})")
                w("ON CONFLICT (dimension_id, code) DO UPDATE SET title = EXCLUDED.title, help_text = EXCLUDED.help_text,")
                w("    scorable = EXCLUDED.scorable, required = EXCLUDED.required, sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;")
                w("INSERT INTO assessment_option (id, question_id, code, label, score, sort_order, metadata) VALUES")
                rows = [f"    ({q(o['id'])}, {q(qu['id'])}, {q(o['code'])}, {q(o['label'])}, "
                        f"{'NULL' if o['score'] is None else o['score']}, {o['sortOrder']}, {j(o['metadata'])})"
                        for o in qu["options"]]
                w(",\n".join(rows))
                w("ON CONFLICT (question_id, code) DO UPDATE SET label = EXCLUDED.label, score = EXCLUDED.score,")
                w("    sort_order = EXCLUDED.sort_order, metadata = EXCLUDED.metadata;")
        w("")

    # ----- Luật -----
    rules = []
    cat_of = {qu["code"]: c["code"] for c in t["categories"] for d in c["dimensions"] for qu in d["questions"]}
    impact_of = {qu["code"]: qu["metadata"]["impact"] for qu in allq if qu["scorable"]}
    ins_payload = {}
    rec_payload = {}

    for code, a in B.ACTIONS.items():
        ins_title, rec_title = B.TITLES[code]
        imp = impact_of[code]
        cat = cat_of[code]
        ins_payload[code] = {"title": ins_title, "body": a["finding"],
                             "severity": "warning" if imp >= 1.1 else "info",
                             "scopeType": "category", "scopeCode": cat}
        rules.append(dict(
            code=f"V2_INS_{code}", name=f"V2 {code} thấp — phát hiện", expr=f"response.{code}.score <= 2",
            type="insight", prio=60 + round(imp * 20), payload=ins_payload[code]))
        body = f"Tuần này: {a['this_week']}\nCách đo: {a['metric']}"
        payload = {"title": rec_title, "body": body, "priority": rec_rank(imp),
                   "estimateHint": a["metric"] if len(a["metric"]) <= 100 else a["metric"][:97] + "…"}
        if a["novixa"]:
            payload["body"] += f"\nNếu muốn làm tự động: {a['novixa']}"
            payload["productArea"] = B.PRODUCT_AREA[cat]
        rec_payload[code] = payload
        rules.append(dict(
            code=f"V2_REC_{code}", name=f"V2 {code} thấp — việc nên làm", expr=f"response.{code}.score <= 2",
            type="recommendation", prio=60 + round(imp * 20), payload=payload))

    for pain, questions in B.PAIN_QUESTIONS.items():
        for code in questions:
            imp = impact_of[code]
            rules.append(dict(
                code=f"V2_PINS_{pain}_{code}", name=f"V2 {pain} {code} — phát hiện theo nỗi đau",
                expr=f"response.P3.option_code = '{pain}' AND response.{code}.score <= 2",
                type="insight", prio=98 if imp >= 1.1 else 97, payload=ins_payload[code]))
            prec = dict(rec_payload[code])
            prec["priority"] = 1
            rules.append(dict(
                code=f"V2_PREC_{pain}_{code}", name=f"V2 {pain} {code} — việc nên làm theo nỗi đau",
                expr=f"response.P3.option_code = '{pain}' AND response.{code}.score <= 2",
                type="recommendation", prio=95, payload=prec))

    # Ước tính bằng tiền: tính sẵn cho từng tổ hợp đáp án (bộ máy luật chỉ so sánh, không tính toán)
    mp = B.MIDPOINTS
    est_exp, est_out = B.ESTIMATES
    for opt, v in mp["I2"].items():
        if v < 1_000_000:
            continue
        yearly = "trên 84 triệu đồng" if opt == "EXP_GT7M" else money(v * 12)
        rules.append(dict(
            code=f"V2_EST_EXPIRED_{opt}", name="V2 ước tính hàng hết hạn/năm",
            expr=f"response.I2.option_code = '{opt}'", type="insight", prio=99,
            payload={"title": f"Hàng hết hạn: khoảng {yearly} mỗi năm",
                     "body": est_exp["text"].format(value=yearly) + " " + est_exp["caveat"],
                     "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}))
    for i3, n in mp["I3"].items():
        if n < 2:
            continue
        for p5, aov in mp["P5"].items():
            val = money(n * 52 * aov)
            rules.append(dict(
                code=f"V2_EST_STOCKOUT_{i3}_{p5}", name="V2 ước tính tiền bán bị lọt do hết hàng/năm",
                expr=f"response.I3.option_code = '{i3}' AND response.P5.option_code = '{p5}'",
                type="insight", prio=96,
                payload={"title": f"Hết hàng: có thể lọt tới {val} mỗi năm",
                         "body": est_out["text"].format(value=val) + " " + est_out["caveat"],
                         "severity": "warning", "scopeType": "overall", "scopeCode": "INVENTORY"}))

    w("-- Luật phát hiện / việc nên làm / ước tính tiền")
    w("INSERT INTO assessment_rule (id, template_id, code, name, expression, action_type, action_payload, priority, is_active) VALUES")
    w(",\n".join(
        f"    ({q(B.uid('rule', r['code']))}, {q(tid)}, {q(r['code'])}, {q(r['name'])}, {q(r['expr'])}, "
        f"{q(r['type'])}, {j(r['payload'])}, {r['prio']}, TRUE)" for r in rules))
    w("ON CONFLICT (template_id, code) DO UPDATE SET name = EXCLUDED.name, expression = EXCLUDED.expression,")
    w("    action_type = EXCLUDED.action_type, action_payload = EXCLUDED.action_payload,")
    w("    priority = EXCLUDED.priority, is_active = EXCLUDED.is_active;")
    w("")
    w("-- Mức trưởng thành: dùng lại thang của V1 (theo điểm tổng 1–4, không phụ thuộc câu hỏi)")
    w("INSERT INTO assessment_maturity_level (template_id, vertical_code, level, code, name, description, score_min, score_max, sort_order, is_active)")
    w(f"SELECT {q(tid)}, vertical_code, level, code, name, description, score_min, score_max, sort_order, is_active")
    w(f"FROM assessment_maturity_level WHERE template_id = {q(V1_TEMPLATE_ID)}")
    w("ON CONFLICT (template_id, vertical_code, level) DO NOTHING;")
    w("")
    w("-- Nguyên nhân gốc V2: bản nháp, chờ dược sĩ duyệt (xem PHARMACY_V2.review.md)")
    w("INSERT INTO assessment_root_cause_kb (")
    w("    id, template_id, question_code, category_code, trigger_expression,")
    w("    cause_code, cause_title, cause_body, evidence_hint, sort_order")
    w(") VALUES")
    rc_rows = []
    for i, (code, cat, cause, title, evidence) in enumerate(B.ROOT_CAUSES, 1):
        rc_rows.append(
            f"    ({q(B.uid('rc', cause))}, {q(tid)}, {q(code)}, {q(cat)}, "
            f"{q(f'response.{code}.score <= 2')}, {q(cause)}, {q(title)}, "
            f"{q(B.ACTIONS[code]['finding'])}, {q(evidence)}, {i * 10})")
    w(",\n".join(rc_rows))
    w("ON CONFLICT (template_id, cause_code) DO UPDATE SET")
    w("    question_code = EXCLUDED.question_code,")
    w("    category_code = EXCLUDED.category_code,")
    w("    trigger_expression = EXCLUDED.trigger_expression,")
    w("    cause_title = EXCLUDED.cause_title,")
    w("    cause_body = EXCLUDED.cause_body,")
    w("    evidence_hint = EXCLUDED.evidence_hint,")
    w("    sort_order = EXCLUDED.sort_order;")
    w("")
    w("COMMIT;")

    sql = "\n".join(out) + "\n"
    open("447_kap_pharmacy_v2.sql", "w", encoding="utf-8").write(sql)
    print(f"447_kap_pharmacy_v2.sql: {len(rules)} luật, {len(sql.splitlines())} dòng")
    long = [r["code"] for r in rules if len(r["payload"]["title"]) > 255]
    if long: print("Tiêu đề quá dài:", long); sys.exit(1)


if __name__ == "__main__":
    main()
