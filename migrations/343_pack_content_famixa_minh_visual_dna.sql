-- KitPlatform 343: attach CHAR-001_MINH_VISUAL_DNA_V1 as DRAFT on current Minh version.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not LOCK character. Does not generate Master. Does not touch Golden Shot.
-- Skips if DNA is already REVIEW/APPROVED or character is LOCKED.

UPDATE pack_content.famixa_character_version v
SET canon_json = jsonb_set(
    jsonb_set(
      canon_json,
      '{identity,currentEra}',
      '"ERA-01"'::jsonb,
      true
    ),
    '{visualDna}',
    $json${
      "documentId":"CHAR-001_MINH_VISUAL_DNA_V1",
      "status":"DRAFT",
      "characterId":"CHAR-001",
      "era":"ERA-01",
      "age":11,
      "version":"V1",
      "role":"Main Child / Primary Protagonist",
      "visualStyle":"FAMIXA_VISUAL_STYLE",
      "whoVisually":"Cậu bé 11 tuổi trong gia đình hiện đại. Đời thường → dễ đồng cảm → có chiều sâu → dễ tổn thương → dễ yêu quý. “Có thể là một đứa trẻ mình từng gặp” nhưng vẫn nhận diện Famixa. Không hot boy, không trẻ QC, không anime, không photoreal, không mặt hoàn hảo.",
      "identityPrinciple":"Stylized Cinematic Human Character — giữa photoreal và cartoon. Anatomy hợp lý, biểu cảm tự nhiên, ánh sáng điện ảnh; mặt / silhouette / tỷ lệ / rendering stylized. Không photoreal. Không cartoon.",
      "headShape":"Mặt trẻ, mềm, hơi bầu nhưng không tròn hết. Trán tương đối rộng. Cằm nhỏ mềm. Má đầy tự nhiên tuổi 11. Không góc cạnh teen. Không baby face quá.",
      "face":"soft slightly oval child face, wide-ish forehead, small chin, natural 11yo cheeks — intelligent, sensitive, inward, vulnerable",
      "eyeShape":"Mắt tương đối lớn nhưng không anime. Ánh nhìn rõ. Mí tự nhiên. Khoảng cách cân. Iris stylized vừa. Không glassy / AI doll. Diễn bằng ánh mắt.",
      "eyes":"relatively large natural eyes, clear gaze, not anime, not doll-glass; acting lives in the eyes",
      "eyebrows":"natural, slightly soft, not thick, not sharp — confused → worried → frustrated → angry → hurt without cartoon brows",
      "noseShape":"Nhỏ, tự nhiên, bridge mềm, không sắc, không mũi người lớn. Ổn định front / 3/4 / side.",
      "nose":"small soft child nose, stable across angles",
      "mouthShape":"Nhỏ đến trung bình, môi tự nhiên. Không cười QC. Không quá dày/mỏng. Im lặng, mím, do dự, buồn, kìm, cười nhẹ. Subtle > exaggerated.",
      "mouth":"small-to-medium natural mouth; default quiet; no ad smile",
      "hairLock":"Tóc đen / đen nâu, kiểu trẻ hiện đại, gọn nhưng không hoàn hảo, volume tự nhiên, lệch nhẹ được. Shape mái = Canon. AI không đổi kiểu / độ dài / chân tóc / silhouette.",
      "hair":"black or dark-brown modern child hair, neat but not perfect, natural volume, slight asymmetry",
      "hairStyle":"modern child cut; silhouette locked",
      "skin":"natural soft stylized skin — no pores photoreal, no plastic, no glossy beauty filter",
      "bodyProportion":"Tỷ lệ trẻ rõ: đầu lớn hơn người lớn, vai nhỏ, tay chân trẻ, mảnh vừa. Không cơ. Không thân teen. Không adult body + child face.",
      "body":"clear 11-year-old proportion, small shoulders, slim, not teen, not adult body",
      "headToBody":"Child proportion — larger head than adult scale. Not chibi. Not photoreal adult ratio.",
      "height":"child 11",
      "build":"slim child",
      "posture":"Hơi khép, tự nhiên, không đứng model. Vai hơi cụp khi buồn, mở hơn khi vui. Thường: reserved. Bị trách: vai hạ, nhìn giảm, thu người. Tổn thương: đóng, ít động, tránh mắt.",
      "emotionalSignature":"Cố giữ cảm xúc thay vì bộc lộ ngay: bị mắng → không khóc ngay → im → nhìn xuống → cố bình thường → cảm xúc lộ dần.",
      "signatureExpression":"Holds emotion before showing it. Eyes and posture first. No instant cartoon cry.",
      "wardrobeBaseline":"School: đồng phục sạch, đơn giản, không fashionized. Home: T-shirt + short/quần dài trẻ, màu giản dị. Đổi đồ = WARDROBE ASSET, không để model tự bịa.",
      "defaultClothing":"HOME",
      "colorPersonality":"Xanh dịu, xanh xám, beige, trắng, màu tự nhiên. Không neon, không chói, không gradient thời trang, không màu QC.",
      "color":"soft blue / grey-blue / beige / white / natural — no neon",
      "distinctiveFeatures":"gaze-acting eyes; quiet mouth; reserved posture; locked hair silhouette; 11yo proportion",
      "recognitionTests":["A Front → 3/4 same Minh","B Neutral → Sad same Minh","C School → Home clothes same Minh","D Bright → Dark lighting same Minh","E Close-up → Full body same Minh","F ERA-01 → ERA-02 recognizable grown Minh (later)"],
      "generationPriority":["FAMIXA VISUAL STYLE SYSTEM V1","CHAR-001 VISUAL DNA V1","ERA-01","MASTER REFERENCE","SCENE","SHOT"],
      "silhouette":"Đầu + tóc + vai + tỷ lệ + posture — đọc được khi bỏ quần áo. Không phụ thuộc wardrobe.",
      "lineStyle":"Stylized cinematic rendering — not photoreal, not cartoon line.",
      "dimension":"Believable volume, stylized face/identity — not 3D scan, not flat cartoon.",
      "texture":"Stylized skin/cloth. No pore-level photoreal. No plastic gloss.",
      "lighting":"Cinematic, motivated by scene. Not beauty light.",
      "realism":"Stylized cinematic human. Not photoreal child. Not cartoon.",
      "emotionRead":"Eyes first, then brows, mouth, posture, negative space. Hold before release.",
      "cuteLevel":"Everyday, not mascot-cute. Not ad-child. Depth over prettiness.",
      "immutableTraits":["CHAR-001 identity persists ERA-01 → ERA-02 → ERA-03","Hair silhouette / hairline logic is Canon","Eye identity and basic face geometry persist across eras","Emotional character: holds feeling before showing it","Child proportion at ERA-01 — never adult body + child face","Stylized cinematic — not photoreal, anime, cartoon, idol, model","Do not use Golden SH01-01 / take-01.mp4 as face lock"],
      "forbidden":["photorealistic real child","anime","cartoon","idol / model / hot boy","superhero","generic AI boy","adult body with child face","overly cute mascot","plastic 3D","đổi mặt / đổi tóc / đổi tỷ lệ / đổi tuổi","thêm hoặc xóa đặc điểm nhận diện","Golden Shot / take-01 as Master"]
    }$json$::jsonb,
    true
  )
FROM pack_content.famixa_character c
WHERE v.character_id = c.id
  AND c.character_code = 'CHAR-001'
  AND v.is_current_canon
  AND c.lifecycle <> 'locked'
  AND NOT (
    v.canon_json->'visualDna'->>'documentId' = 'CHAR-001_MINH_VISUAL_DNA_V1'
    AND COALESCE(v.canon_json->'visualDna'->>'status', '') IN ('REVIEW', 'APPROVED')
  );

UPDATE pack_content.famixa_character
SET current_era = 'ERA-01', updated_at = NOW()
WHERE character_code = 'CHAR-001' AND lifecycle <> 'locked';
