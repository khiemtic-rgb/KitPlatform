-- KitPlatform 332: CHAR-001 Minh Character Brief V1 (human design)
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does NOT lock CHAR-001. Does NOT overwrite visualDna / references / voiceId / master PNG.
-- Does NOT create Ông / Bà / Em gái IDs. CHAR-004 An stays friend (not sister).

UPDATE pack_content.famixa_character_version v
SET canon_json = jsonb_set(
  v.canon_json
    || $json${
      "brief": {
        "documentId": "FAMIXA-CHAR-001-BRIEF-V1",
        "characterId": "CHAR-001",
        "status": "BRIEF",
        "readyFor": "VISUAL_DESIGN",
        "statement": "Minh is an ordinary Vietnamese boy growing up, curious, confident, mildly stubborn, sensitive to adults' words, responsible, and deeply wanting to be trusted. He is not weak or overly dramatic. His emotions are subtle and expressed primarily through his eyes, mouth, posture, silence, and small behavioral changes.",
        "oneLine": "Một cậu bé đang lớn, muốn được tin tưởng — nhưng vẫn chưa biết cách nói ra điều đó.",
        "emotionalCore": "Minh muốn được tin tưởng.",
        "notA": ["gifted child", "perfect child", "cartoon comedy kid", "con nhà người ta", "always pitiful", "always crying / head-down / weak"],
        "notLocked": ["face", "hair", "skin", "height", "weight", "outfit", "voiceId", "masterReference", "expressionReference"],
        "nextStep": "FAMIXA-CHAR-001-VISUAL-DNA-SPEC-V1",
        "visualDirection": "Famixa look: stylized-human cinematic family drama. Everyday Vietnamese boy with a recognizable face — not kid-model, K-drama, fashion, cartoon, anime, or generic any-boy.",
        "familyNote": "Ông / Bà / Em gái chưa có Character ID. CHAR-002 = Nam (Bố), CHAR-003 = Linh (Mẹ). CHAR-004 = An (bạn, mention-only) — không phải em gái."
      },
      "personality": {
        "core": ["curious", "confident", "mildly stubborn", "sensitive", "responsible", "wants independence"],
        "strengths": ["thinks for himself", "observes", "learns from experience", "can change", "can love", "can take responsibility", "gets back up after disappointment", "has agency"],
        "weaknesses": ["mildly stubborn", "easily hurt by words", "reacts before thinking", "cannot yet name feelings", "tries to prove himself too fast"],
        "fears": ["disappointing parents", "being looked down on", "being compared", "being told he is not enough", "speaking and not being heard", "trying and not being recognized"],
        "needs": ["to be trusted", "to be seen as growing up"],
        "desires": ["parents see that I am growing"],
        "values": ["trust", "recognition", "independence without abandoning family"],
        "triggers": ["parents' words", "a look", "comparison", "disappointment", "being doubted", "being treated as too young"],
        "emotionalPatterns": ["EXCITED / BRIGHT", "DISAPPOINTED", "CLOSED / ALONE", "DETERMINED", "VULNERABLE", "UNDERSTANDING", "OPEN / WARM"]
      },
      "personalityDna": ["curious", "confident", "mildly stubborn", "sensitive", "responsible", "wants independence"],
      "behaviorDna": [
        {"when": "happy", "does": "eyes look for the adult; body open; real child smile — not ad smile"},
        {"when": "hurt", "does": "eyes look away; stands still; smile fades; may fold the paper; becomes quiet rather than crying"},
        {"when": "angry", "does": "holds a longer look; blinks less; body stiffer; speaks less"},
        {"when": "sad", "does": "eyes lose energy; may press lips; swallows the feeling; says không sao"},
        {"when": "wants recognition", "does": "looks at the other person and waits"},
        {"when": "refused", "does": "shoulders pull in; may go quiet, close the door, or pretend nothing happened"},
        {"when": "determined", "does": "stance firmer; not only endures — acts"},
        {"when": "embarrassed", "does": "looks down; may hesitate mid-sentence"}
      ],
      "plannedFamily": [
        {"role": "grandfather", "label": "Ông"},
        {"role": "grandmother", "label": "Bà"},
        {"role": "sister", "label": "Em gái"}
      ],
      "evolution": {
        "initialEra": "A11",
        "eras": [
          {"era": "A11", "status": "active"},
          {"era": "A13", "status": "planned"},
          {"era": "A16", "status": "planned"},
          {"era": "A18", "status": "planned"},
          {"era": "A23", "status": "planned"}
        ]
      },
      "relationships": [
        {"to": "CHAR-003", "type": "mother"},
        {"to": "CHAR-002", "type": "father"},
        {"to": "CHAR-004", "type": "friend"}
      ]
    }$json$::jsonb,
  '{identity}',
  COALESCE(v.canon_json->'identity', '{}'::jsonb) || $json${
    "characterId": "CHAR-001",
    "name": "Minh",
    "role": "Main Child",
    "familyRole": "Son",
    "characterType": "CORE",
    "currentAge": 11,
    "initialAge": 11,
    "currentEra": "A11",
    "biography": "Ordinary Vietnamese boy, 11, growing up. Emotional center of Famixa. Wants to be trusted. Not a perfect child, not a pitiful child."
  }$json$::jsonb
)
FROM pack_content.famixa_character c
WHERE v.character_id = c.id
  AND c.character_code = 'CHAR-001'
  AND v.id = c.current_version_id;

UPDATE pack_content.famixa_character
SET role = 'Main Child', updated_at = NOW()
WHERE character_code = 'CHAR-001'
  AND lifecycle <> 'locked';

INSERT INTO pack_content.famixa_character_audit (
    id, character_id, character_code, version, field_changed, old_value, new_value,
    changed_by, changed_at, reason, approval
)
SELECT
    '019f3320-0001-7000-8000-000000000001',
    c.id,
    'CHAR-001',
    COALESCE(v.version, 'V1'),
    'brief',
    NULL,
    'FAMIXA-CHAR-001-BRIEF-V1',
    'brief-v1',
    NOW(),
    'Attach Minh Character Brief V1. Visual / Master / Voice remain unlocked.',
    NULL
FROM pack_content.famixa_character c
JOIN pack_content.famixa_character_version v ON v.id = c.current_version_id
WHERE c.character_code = 'CHAR-001'
  AND NOT EXISTS (
    SELECT 1 FROM pack_content.famixa_character_audit a
    WHERE a.id = '019f3320-0001-7000-8000-000000000001'
  );
