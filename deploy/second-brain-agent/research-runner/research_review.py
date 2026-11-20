"""Read an evaluation and propose the next experiment. Proposals stay unapproved.

The official catalog is the three locked strategies. This module cannot add
to that catalog and the runner does not read proposal files when it evaluates.
"""

from __future__ import annotations

from decimal import Decimal

OFFICIAL_EXPERIMENTS = (
    "EXP-TREND-SMA-10-30-V1",
    "EXP-BREAKOUT-DONCHIAN-20-V1",
    "EXP-REVERSION-RANGE-20-V1",
)


def official_catalog() -> tuple[str, ...]:
    return OFFICIAL_EXPERIMENTS


def admit_for_official_evaluation(experiment_id: str) -> bool:
    return experiment_id in OFFICIAL_EXPERIMENTS


def host_read_constraints() -> dict:
    return {
        "reader": "FileResearchPublicationReader.Read",
        "reads_disk_on_each_call": True,
        "root_bound_at_process_start": "PersonalIntelligence__ResearchPublication__Root",
        "running_root": "/opt/second-brain-research-publication-v1",
        "running_root_is_real_directory": True,
        "switch_without_restart": False,
        "reason": "The configured root is a real directory captured when the process starts. A later immutable publication is invisible until that root changes. Changing it restarts second-brain-agent. This run does not change the root.",
        "performed": False,
    }


def review_experiments(experiments: list[dict], funding_coverage: str) -> dict:
    findings = []
    proposals = []
    for item in experiments:
        development = Decimal(item["development"]["net_pnl"])
        out_of_sample = Decimal(item["out_of_sample"]["net_pnl"])
        reasons = []
        if out_of_sample < 0:
            reasons.append("OOS_NET_NEGATIVE")
        if development > 0 and out_of_sample < 0:
            reasons.append("DEVELOPMENT_EDGE_REVERSED")
        if development < 0 and out_of_sample < 0:
            reasons.append("NEGATIVE_IN_BOTH_WINDOWS")
        if funding_coverage != "PUBLIC_FUNDING_COMPLETE":
            reasons.append("FUNDING_DID_NOT_COVER_EXTENSION")
        findings.append({
            "experiment_id": item["experiment_id"],
            "development_net_pnl": item["development"]["net_pnl"],
            "out_of_sample_net_pnl": item["out_of_sample"]["net_pnl"],
            "reasons": reasons,
        })
        proposals.extend(_proposals_for(item["experiment_id"], reasons))
    if funding_coverage != "PUBLIC_FUNDING_COMPLETE":
        proposals.insert(0, {
            "proposal_id": "PROP-COMPLETE-FUNDING-BEFORE-NEXT-SELECTION",
            "kind": "DATA",
            "experiment_id": None,
            "approval_status": "NOT_A_STRATEGY",
            "hypothesis": "Phần lỗ ngoài mẫu đang thiếu funding sau snapshot. Chạy lại đúng ba chiến lược đã khóa sau khi funding phủ hết nến, trước khi thêm chiến lược mới.",
            "enters_official_evaluation": False,
        })
    return {
        "approval_status": "PROPOSED_NOT_APPROVED",
        "selected_strategy": None,
        "official_catalog": list(OFFICIAL_EXPERIMENTS),
        "official_catalog_changed": False,
        "live_orders": False,
        "exchange_orders_sent": 0,
        "findings": findings,
        "proposals": proposals,
        "host_read": host_read_constraints(),
    }


def _proposals_for(experiment_id: str, reasons: list[str]) -> list[dict]:
    if "DEVELOPMENT_EDGE_REVERSED" in reasons and experiment_id == "EXP-TREND-SMA-10-30-V1":
        return [_proposal(
            "PROP-TREND-REGIME-FILTER",
            "EXP-TREND-SMA-10-30-REGIME-V2",
            "SMA 10/30 lời trong mẫu và lỗ ngoài mẫu. Giả thuyết: các lần cắt xuất hiện sau khi đường chậm đã đảo chiều. Thí nghiệm kế tiếp chỉ vào lệnh khi độ dốc đường chậm cùng chiều với tín hiệu cắt.",
        )]
    if "DEVELOPMENT_EDGE_REVERSED" in reasons and experiment_id == "EXP-BREAKOUT-DONCHIAN-20-V1":
        return [_proposal(
            "PROP-DONCHIAN-CONFIRM-CLOSE",
            "EXP-BREAKOUT-DONCHIAN-20-CONFIRM-V2",
            "Phá vùng 20 nến lời trong mẫu và lỗ ngoài mẫu. Giả thuyết: một nến đóng ngoài vùng chưa đủ, vì giá quay lại vùng ở nến sau. Thí nghiệm kế tiếp đòi hai nến đóng liên tiếp ngoài vùng trước khi vào.",
        )]
    if "NEGATIVE_IN_BOTH_WINDOWS" in reasons and experiment_id == "EXP-REVERSION-RANGE-20-V1":
        return [_proposal(
            "PROP-RANGE-DO-NOT-CONTINUE",
            "EXP-REVERSION-RANGE-20-V2",
            "Range 20 lỗ cả trong mẫu và ngoài mẫu. Giả thuyết đi ngang không bù phí ở cửa sổ này. Không đưa biến thể biên độ khác vào đánh giá chính thức cho đến khi có phê duyệt riêng.",
        )]
    return []


def _proposal(proposal_id: str, experiment_id: str, hypothesis: str) -> dict:
    return {
        "proposal_id": proposal_id,
        "kind": "STRATEGY",
        "experiment_id": experiment_id,
        "approval_status": "PROPOSED_NOT_APPROVED",
        "hypothesis": hypothesis,
        "enters_official_evaluation": admit_for_official_evaluation(experiment_id),
    }
