<#
.SYNOPSIS
  Generates migrations/296_pharmacy_symptom_taxonomy_seed.sql from Novixa Symptom Taxonomy V1.

.EXAMPLE
  .\scripts\generate-pharmacy-symptom-seed.ps1
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\migrations\296_pharmacy_symptom_taxonomy_seed.sql")
)

$ErrorActionPreference = "Stop"

function Escape-Sql([string]$Value) {
    if ($null -eq $Value) { return "NULL" }
    return "'" + ($Value -replace "'", "''") + "'"
}

function Escape-SqlArray([string[]]$Values) {
    if (-not $Values -or $Values.Count -eq 0) { return "ARRAY[]::TEXT[]" }
    $items = $Values | ForEach-Object { Escape-Sql $_ }
    return "ARRAY[$($items -join ', ')]"
}

function Get-StableUuid([string]$Namespace, [string]$Key) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes("$Namespace`:$Key")
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($hash, $guidBytes, 16)
    $guidBytes[6] = ($guidBytes[6] -band 0x0F) -bor 0x40
    $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80
    return ([guid]$guidBytes).ToString().ToLowerInvariant()
}

$Categories = @(
    @{ Code = "respiratory";       LabelVi = "Hô hấp";                    Sort = 10 }
    @{ Code = "fever_pain";        LabelVi = "Đau / sốt";                 Sort = 20 }
    @{ Code = "digestive";         LabelVi = "Tiêu hóa";                  Sort = 30 }
    @{ Code = "ent";               LabelVi = "Tai mũi họng";              Sort = 40 }
    @{ Code = "eye";               LabelVi = "Mắt";                       Sort = 50 }
    @{ Code = "oral_dental";       LabelVi = "Răng miệng";                Sort = 60 }
    @{ Code = "skin_allergy";     LabelVi = "Da / dị ứng";               Sort = 70 }
    @{ Code = "musculoskeletal";  LabelVi = "Cơ xương khớp";             Sort = 80 }
    @{ Code = "urinary";           LabelVi = "Tiết niệu";                 Sort = 90 }
    @{ Code = "women_health";      LabelVi = "Sức khỏe phụ nữ";           Sort = 100 }
    @{ Code = "men_health";        LabelVi = "Sức khỏe nam giới";         Sort = 110 }
    @{ Code = "child_health";      LabelVi = "Sức khỏe trẻ em";           Sort = 120 }
    @{ Code = "neuro_sleep";       LabelVi = "Thần kinh / giấc ngủ";      Sort = 130 }
    @{ Code = "nutrition_general"; LabelVi = "Dinh dưỡng / tổng quát";    Sort = 140 }
    @{ Code = "chronic_condition"; LabelVi = "Bệnh mạn tính";             Sort = 150 }
    @{ Code = "injury_first_aid";  LabelVi = "Chấn thương / sơ cứu";      Sort = 160 }
    @{ Code = "cardiovascular";   LabelVi = "Tim mạch";                  Sort = 170 }
    @{ Code = "other";             LabelVi = "Khác";                      Sort = 180 }
)

# code, taxonomy_ref, name_vi, category_code, consultation_mode, sort_order
$Symptoms = @(
    # respiratory (18)
    ,@("cough",              "RESP-001", "Ho",                        "respiratory", "otc_assist",     1)
    ,@("cough_dry",          "RESP-002", "Ho khan",                   "respiratory", "otc_assist",     2)
    ,@("cough_phlegm",       "RESP-003", "Ho có đờm",                 "respiratory", "otc_assist",     3)
    ,@("runny_nose",         "RESP-004", "Sổ mũi",                    "respiratory", "otc_assist",     4)
    ,@("nasal_congestion",   "RESP-005", "Nghẹt mũi",                  "respiratory", "otc_assist",     5)
    ,@("sore_throat",        "RESP-006", "Đau họng",                   "respiratory", "otc_assist",     6)
    ,@("sneezing",           "RESP-007", "Hắt hơi",                    "respiratory", "otc_assist",     7)
    ,@("common_cold",        "RESP-008", "Cảm lạnh / cảm cúm",         "respiratory", "otc_assist",     8)
    ,@("flu_symptoms",       "RESP-009", "Triệu chứng cúm",            "respiratory", "otc_assist",     9)
    ,@("sinus_pressure",     "RESP-010", "Áp lực xoang",               "respiratory", "otc_assist",    10)
    ,@("post_nasal_drip",    "RESP-011", "Chảy nước mũi sau",           "respiratory", "otc_assist",    11)
    ,@("shortness_of_breath","RESP-012", "Khó thở",                    "respiratory", "capture_only",  12)
    ,@("wheezing",           "RESP-013", "Thở khò khè",                 "respiratory", "capture_only",  13)
    ,@("breathing_discomfort","RESP-014","Khó thở nhẹ / tức ngực",      "respiratory", "capture_only",  14)
    ,@("chest_tightness",    "RESP-015", "Tức ngực",                   "respiratory", "capture_only",  15)
    ,@("persistent_cough",   "RESP-016", "Ho kéo dài",                 "respiratory", "capture_only",  16)
    ,@("cough_with_blood",   "RESP-017", "Ho ra máu",                  "respiratory", "capture_only",  17)
    ,@("loss_of_smell",      "RESP-018", "Mất khứu giác",              "respiratory", "capture_only",  18)

    # fever_pain (14)
    ,@("fever",               "FEV-001", "Sốt",                        "fever_pain", "otc_assist",      1)
    ,@("headache",            "FEV-002", "Đau đầu",                    "fever_pain", "otc_assist",      2)
    ,@("body_ache",           "FEV-003", "Đau cơ / đau nhức",          "fever_pain", "otc_assist",      3)
    ,@("chills",              "FEV-004", "Rét run",                    "fever_pain", "otc_assist",      4)
    ,@("mild_fever",          "FEV-005", "Sốt nhẹ",                    "fever_pain", "otc_assist",      5)
    ,@("sore_muscles",        "FEV-006", "Đau cơ vãn",                 "fever_pain", "otc_assist",      6)
    ,@("toothache",           "FEV-007", "Đau răng",                   "fever_pain", "otc_assist",      7)
    ,@("period_pain",         "FEV-008", "Đau bụng kinh",              "fever_pain", "capture_only",    8)
    ,@("high_fever",          "FEV-009", "Sốt cao",                    "fever_pain", "capture_only",    9)
    ,@("migraine",            "FEV-010", "Đau nửa đầu",                "fever_pain", "capture_only",   10)
    ,@("fever_with_rash",     "FEV-011", "Sốt kèm phát ban",           "fever_pain", "capture_only",   11)
    ,@("night_sweats",        "FEV-012", "Đổ mồ hôi đêm",              "fever_pain", "capture_only",   12)
    ,@("facial_pain",         "FEV-013", "Đau mặt / viêm xoang",       "fever_pain", "otc_assist",     13)
    ,@("general_pain",        "FEV-014", "Đau toàn thân",              "fever_pain", "otc_assist",     14)

    # digestive (18)
    ,@("diarrhea",            "DIG-001", "Tiêu chảy",                  "digestive", "otc_assist",        1)
    ,@("nausea",              "DIG-002", "Buồn nôn",                   "digestive", "otc_assist",        2)
    ,@("vomiting",            "DIG-003", "Nôn / ói",                   "digestive", "otc_assist",        3)
    ,@("abdominal_pain",      "DIG-004", "Đau bụng",                   "digestive", "otc_assist",        4)
    ,@("heartburn",           "DIG-005", "Ợ nóng",                     "digestive", "otc_assist",        5)
    ,@("reflux",              "DIG-006", "Trào ngược / ợ chua",        "digestive", "otc_assist",        6)
    ,@("constipation",        "DIG-007", "Táo bón",                    "digestive", "otc_assist",        7)
    ,@("bloating",            "DIG-008", "Đầy bụng",                   "digestive", "otc_assist",        8)
    ,@("indigestion",         "DIG-009", "Khó tiêu",                   "digestive", "otc_assist",        9)
    ,@("stomach_cramps",      "DIG-010", "Co thắt dạ dày",             "digestive", "otc_assist",       10)
    ,@("gas",                 "DIG-011", "Đầy hơi",                    "digestive", "otc_assist",       11)
    ,@("food_poisoning",      "DIG-012", "Ngộ độc thực phẩm",          "digestive", "capture_only",     12)
    ,@("loss_of_appetite",    "DIG-013", "Chán ăn",                    "digestive", "capture_only",     13)
    ,@("blood_in_stool",      "DIG-014", "Đi ngoài ra máu",            "digestive", "capture_only",     14)
    ,@("black_stool",         "DIG-015", "Phân đen",                   "digestive", "capture_only",     15)
    ,@("severe_abdominal_pain","DIG-016","Đau bụng dữ dội",             "digestive", "capture_only",     16)
    ,@("vomiting_blood",      "DIG-017", "Nôn ra máu",                 "digestive", "capture_only",     17)
    ,@("dehydration_signs",   "DIG-018", "Dấu hiệu mất nước",          "digestive", "capture_only",     18)

    # ent (13) - ENT-006 sore_throat omitted; alias links to respiratory sore_throat
    ,@("ear_pain",            "ENT-001", "Đau tai",                    "ent", "otc_assist",                1)
    ,@("ear_fullness",        "ENT-002", "Tai bị bít / ù",             "ent", "otc_assist",                2)
    ,@("ear_discharge",       "ENT-003", "Tai chảy dịch / mủ",         "ent", "capture_only",              3)
    ,@("hearing_loss",        "ENT-004", "Giảm thính lực",             "ent", "capture_only",              4)
    ,@("tinnitus",            "ENT-005", "Ù tai",                      "ent", "capture_only",              5)
    ,@("swollen_lymph_nodes", "ENT-007", "Hạch cổ sưng",               "ent", "capture_only",              6)
    ,@("throat_clearing",     "ENT-008", "Khản tiếng / vướng họng",    "ent", "otc_assist",                7)
    ,@("voice_hoarseness",    "ENT-009", "Khàn tiếng",                 "ent", "otc_assist",              8)
    ,@("nasal_bleeding",      "ENT-010", "Chảy máu mũi",               "ent", "capture_only",              9)
    ,@("sinus_pain",          "ENT-011", "Đau xoang",                  "ent", "otc_assist",               10)
    ,@("ear_itch",            "ENT-012", "Ngứa tai",                   "ent", "otc_assist",               11)
    ,@("blocked_ear",         "ENT-013", "Bịt tai / ù tai",            "ent", "otc_assist",               12)
    ,@("swallowing_discomfort","ENT-014","Khó nuốt / vướng họng",      "ent", "capture_only",             13)

    # eye (10)
    ,@("red_eye",             "EYE-001", "Đỏ mắt",                     "eye", "otc_assist",                1)
    ,@("itchy_eye",           "EYE-002", "Ngứa mắt",                   "eye", "otc_assist",                2)
    ,@("watery_eye",          "EYE-003", "Chảy nước mắt",              "eye", "otc_assist",                3)
    ,@("eye_pain",            "EYE-004", "Đau mắt",                    "eye", "capture_only",              4)
    ,@("dry_eye",             "EYE-005", "Khô mắt",                    "eye", "otc_assist",                5)
    ,@("eye_discharge",       "EYE-006", "Mắt có ghèn / dịch",         "eye", "capture_only",              6)
    ,@("blurred_vision",      "EYE-007", "Nhìn mờ",                    "eye", "capture_only",              7)
    ,@("eye_swelling",        "EYE-008", "Sưng mí mắt",                "eye", "capture_only",              8)
    ,@("foreign_body_eye",    "EYE-009", "Dị vật trong mắt",           "eye", "capture_only",              9)
    ,@("sudden_vision_loss",  "EYE-010", "Mất thị lực đột ngột",       "eye", "capture_only",             10)

    # oral_dental (10)
    ,@("mouth_ulcer",         "ORA-001", "Loét miệng",                 "oral_dental", "otc_assist",       1)
    ,@("gum_pain",            "ORA-002", "Đau nướu",                   "oral_dental", "otc_assist",       2)
    ,@("bleeding_gums",       "ORA-003", "Chảy máu chân răng",         "oral_dental", "capture_only",     3)
    ,@("tooth_sensitivity",   "ORA-004", "Ê buốt răng",                "oral_dental", "otc_assist",       4)
    ,@("bad_breath",          "ORA-005", "Hôi miệng",                  "oral_dental", "otc_assist",       5)
    ,@("dry_mouth",           "ORA-006", "Khô miệng",                  "oral_dental", "otc_assist",       6)
    ,@("oral_thrush",         "ORA-007", "Nấm miệng",                  "oral_dental", "capture_only",     7)
    ,@("jaw_pain",            "ORA-008", "Đau hàm",                    "oral_dental", "capture_only",     8)
    ,@("dental_abscess",      "ORA-009", "Áp xe răng",                 "oral_dental", "capture_only",     9)
    ,@("broken_tooth",        "ORA-010", "Gãy răng",                   "oral_dental", "capture_only",    10)

    # skin_allergy (15)
    ,@("allergy",             "SKI-001", "Dị ứng",                     "skin_allergy", "otc_assist",      1)
    ,@("itchy_skin",          "SKI-002", "Ngứa da",                    "skin_allergy", "otc_assist",      2)
    ,@("rash",                "SKI-003", "Phát ban",                   "skin_allergy", "otc_assist",      3)
    ,@("hives",               "SKI-004", "Mề đay / mẩn đỏ",            "skin_allergy", "otc_assist",      4)
    ,@("swelling",            "SKI-005", "Sưng / phù nề",              "skin_allergy", "capture_only",    5)
    ,@("insect_bite",         "SKI-006", "Côn trùng cắn",              "skin_allergy", "otc_assist",      6)
    ,@("dry_skin",            "SKI-007", "Da khô",                     "skin_allergy", "otc_assist",      7)
    ,@("eczema_flare",        "SKI-008", "Viêm da cơ địa",             "skin_allergy", "capture_only",    8)
    ,@("sunburn",             "SKI-009", "Cháy nắng",                  "skin_allergy", "otc_assist",      9)
    ,@("acne",                "SKI-010", "Mụn trứng cá",               "skin_allergy", "otc_assist",     10)
    ,@("fungal_skin",         "SKI-011", "Nấm da",                     "skin_allergy", "capture_only",   11)
    ,@("skin_infection",      "SKI-012", "Nhiễm trùng da",             "skin_allergy", "capture_only",   12)
    ,@("blister",             "SKI-013", "Bọng nước",                  "skin_allergy", "otc_assist",     13)
    ,@("contact_dermatitis",  "SKI-014", "Viêm da tiếp xúc",           "skin_allergy", "capture_only",   14)
    ,@("severe_allergy",      "SKI-015", "Dị ứng nặng / phù mạch",     "skin_allergy", "capture_only",   15)

    # musculoskeletal (12)
    ,@("back_pain",           "MUS-001", "Đau lưng",                   "musculoskeletal", "otc_assist",   1)
    ,@("joint_pain",          "MUS-002", "Đau khớp",                   "musculoskeletal", "otc_assist",   2)
    ,@("neck_pain",           "MUS-003", "Đau cổ",                     "musculoskeletal", "otc_assist",   3)
    ,@("muscle_cramps",       "MUS-004", "Chuột rút",                  "musculoskeletal", "otc_assist",   4)
    ,@("sprain",              "MUS-005", "Bong gân",                   "musculoskeletal", "otc_assist",   5)
    ,@("shoulder_pain",       "MUS-006", "Đau vai",                    "musculoskeletal", "otc_assist",   6)
    ,@("knee_pain",           "MUS-007", "Đau gối",                    "musculoskeletal", "otc_assist",   7)
    ,@("arthritis_pain",      "MUS-008", "Đau khớp viêm",              "musculoskeletal", "capture_only", 8)
    ,@("muscle_stiffness",    "MUS-009", "Cứng cơ",                    "musculoskeletal", "otc_assist",   9)
    ,@("heel_pain",           "MUS-010", "Đau gót chân",               "musculoskeletal", "otc_assist",  10)
    ,@("gout_pain",           "MUS-011", "Đau gút",                    "musculoskeletal", "capture_only",11)
    ,@("sciatica",            "MUS-012", "Đau thần kinh tọa",          "musculoskeletal", "capture_only",12)

    # urinary (9) - pharmacist_only except urinary_discomfort
    ,@("urinary_discomfort",  "URI-001", "Tiểu buốt / khó chịu",      "urinary", "capture_only",          1)
    ,@("frequent_urination", "URI-002", "Tiểu nhiều",                "urinary", "pharmacist_only",       2)
    ,@("burning_urination",   "URI-003", "Tiểu rắt",                   "urinary", "pharmacist_only",       3)
    ,@("blood_in_urine",      "URI-004", "Tiểu máu",                   "urinary", "pharmacist_only",       4)
    ,@("urinary_retention",   "URI-005", "Bí tiểu",                    "urinary", "pharmacist_only",       5)
    ,@("incontinence",        "URI-006", "Tiểu không tự chủ",          "urinary", "pharmacist_only",       6)
    ,@("kidney_pain",         "URI-007", "Đau thận / hông",            "urinary", "pharmacist_only",       7)
    ,@("uti_symptoms",        "URI-008", "Nghi nhiễm tiết niệu",       "urinary", "pharmacist_only",       8)
    ,@("prostate_urinary",    "URI-009", "Tiểu khó (nam)",             "urinary", "pharmacist_only",       9)

    # women_health (10) - pharmacist_only
    ,@("menstrual_irregular", "WOM-001", "Kinh nguyệt không đều",      "women_health", "pharmacist_only", 1)
    ,@("vaginal_discharge",   "WOM-002", "Khí hư bất thường",          "women_health", "pharmacist_only", 2)
    ,@("vaginal_itch",        "WOM-003", "Ngứa vùng kín",              "women_health", "pharmacist_only", 3)
    ,@("menopause_symptoms",  "WOM-004", "Triệu chứng mãn kinh",       "women_health", "pharmacist_only", 4)
    ,@("contraception_need",  "WOM-005", "Tư vấn tránh thai",          "women_health", "pharmacist_only", 5)
    ,@("pregnancy_test",      "WOM-006", "Nghi mang thai",             "women_health", "pharmacist_only", 6)
    ,@("breast_pain",         "WOM-007", "Đau vú",                     "women_health", "pharmacist_only", 7)
    ,@("yeast_infection",     "WOM-008", "Nấm âm đạo",                 "women_health", "pharmacist_only", 8)
    ,@("morning_sickness",    "WOM-009", "Ốm nghén",                   "women_health", "pharmacist_only", 9)
    ,@("pregnancy_bleeding",  "WOM-010", "Ra máu khi mang thai",       "women_health", "pharmacist_only",10)

    # men_health (5) - pharmacist_only
    ,@("erectile_concern",    "MEN-001", "Rối loạn cương dương",       "men_health", "pharmacist_only",   1)
    ,@("prostate_concern",    "MEN-002", "Tuyến tiền liệt",            "men_health", "pharmacist_only",   2)
    ,@("hair_loss",           "MEN-003", "Rụng tóc",                   "men_health", "pharmacist_only",   3)
    ,@("testicular_pain",     "MEN-004", "Đau tinh hoàn",              "men_health", "pharmacist_only",   4)
    ,@("libido_concern",      "MEN-005", "Giảm ham muốn",              "men_health", "pharmacist_only",   5)

    # child_health (14) - capture_only or pharmacist_only
    ,@("child_fever",         "CHI-001", "Trẻ sốt",                    "child_health", "pharmacist_only",  1)
    ,@("child_cough",         "CHI-002", "Trẻ ho",                     "child_health", "pharmacist_only",  2)
    ,@("child_diarrhea",      "CHI-003", "Trẻ tiêu chảy",              "child_health", "pharmacist_only",  3)
    ,@("child_vomiting",      "CHI-004", "Trẻ nôn",                    "child_health", "pharmacist_only",  4)
    ,@("child_rash",          "CHI-005", "Trẻ phát ban",               "child_health", "pharmacist_only",  5)
    ,@("child_ear_pain",      "CHI-006", "Trẻ đau tai",                "child_health", "pharmacist_only",  6)
    ,@("child_colic",         "CHI-007", "Trẻ colic / đầy hơi",        "child_health", "capture_only",     7)
    ,@("child_teething",      "CHI-008", "Trẻ mọc răng",               "child_health", "capture_only",     8)
    ,@("child_constipation",  "CHI-009", "Trẻ táo bón",                "child_health", "pharmacist_only",  9)
    ,@("child_allergy",       "CHI-010", "Trẻ dị ứng",                 "child_health", "pharmacist_only", 10)
    ,@("infant_feeding",      "CHI-011", "Bú / ăn kém",                "child_health", "pharmacist_only", 11)
    ,@("child_sleep_issue",   "CHI-012", "Trẻ khó ngủ",                "child_health", "capture_only",    12)
    ,@("child_wheezing",      "CHI-013", "Trẻ thở khò khè",            "child_health", "pharmacist_only", 13)
    ,@("child_dehydration",   "CHI-014", "Trẻ mất nước",               "child_health", "pharmacist_only", 14)

    # neuro_sleep (10)
    ,@("insomnia",            "NEU-001", "Mất ngủ / khó ngủ",          "neuro_sleep", "otc_assist",       1)
    ,@("anxiety",             "NEU-002", "Lo âu / căng thẳng",         "neuro_sleep", "capture_only",     2)
    ,@("dizziness",           "NEU-003", "Chóng mặt",                  "neuro_sleep", "otc_assist",       3)
    ,@("fatigue",             "NEU-004", "Mệt mỏi",                    "neuro_sleep", "otc_assist",       4)
    ,@("stress",              "NEU-005", "Stress",                     "neuro_sleep", "capture_only",     5)
    ,@("memory_concern",      "NEU-006", "Lo giảm trí nhớ",            "neuro_sleep", "capture_only",     6)
    ,@("tingling",            "NEU-007", "Tê bì / ngứa ran",            "neuro_sleep", "capture_only",     7)
    ,@("headache_chronic",    "NEU-008", "Đau đầu mạn",                "neuro_sleep", "capture_only",     8)
    ,@("sleep_apnea_signs",   "NEU-009", "Ngáy / ngưng thở khi ngủ",   "neuro_sleep", "capture_only",     9)
    ,@("panic_symptoms",      "NEU-010", "Hoảng loạn",                 "neuro_sleep", "capture_only",    10)

    # nutrition_general (8) - context_only
    ,@("vitamin_deficiency",  "NUT-001", "Thiếu vitamin",              "nutrition_general", "context_only", 1)
    ,@("weight_loss",         "NUT-002", "Sụt cân",                    "nutrition_general", "context_only", 2)
    ,@("weight_gain",         "NUT-003", "Tăng cân",                   "nutrition_general", "context_only", 3)
    ,@("poor_appetite",       "NUT-004", "Ăn kém",                     "nutrition_general", "context_only", 4)
    ,@("supplement_need",     "NUT-005", "Cần bổ sung dinh dưỡng",     "nutrition_general", "context_only", 5)
    ,@("immune_support",      "NUT-006", "Tăng sức đề kháng",          "nutrition_general", "context_only", 6)
    ,@("elderly_nutrition",   "NUT-007", "Dinh dưỡng người cao tuổi",  "nutrition_general", "context_only", 7)
    ,@("pregnancy_nutrition", "NUT-008", "Dinh dưỡng thai kỳ",         "nutrition_general", "context_only", 8)

    # chronic_condition (10) - context_only
    ,@("hypertension",        "CHR-001", "Tăng huyết áp",              "chronic_condition", "context_only", 1)
    ,@("diabetes",            "CHR-002", "Đái tháo đường",             "chronic_condition", "context_only", 2)
    ,@("asthma",              "CHR-003", "Hen suyễn",                  "chronic_condition", "context_only", 3)
    ,@("copd",                "CHR-004", "Phổi tắc nghẽn mạn",          "chronic_condition", "context_only", 4)
    ,@("heart_disease",       "CHR-005", "Bệnh tim mạch",              "chronic_condition", "context_only", 5)
    ,@("kidney_disease",      "CHR-006", "Bệnh thận mạn",              "chronic_condition", "context_only", 6)
    ,@("liver_disease",       "CHR-007", "Bệnh gan",                   "chronic_condition", "context_only", 7)
    ,@("thyroid_disorder",    "CHR-008", "Rối loạn tuyến giáp",        "chronic_condition", "context_only", 8)
    ,@("osteoporosis",        "CHR-009", "Loãng xương",                "chronic_condition", "context_only", 9)
    ,@("chronic_pain",        "CHR-010", "Đau mạn tính",               "chronic_condition", "context_only",10)

    # injury_first_aid (8) - otc_assist for minor
    ,@("minor_cut",           "INJ-001", "Vết cắt nhỏ",                "injury_first_aid", "otc_assist",    1)
    ,@("minor_burn",          "INJ-002", "Bỏng nhẹ",                   "injury_first_aid", "otc_assist",    2)
    ,@("bruise",              "INJ-003", "Bầm tím",                    "injury_first_aid", "otc_assist",    3)
    ,@("sprain_minor",        "INJ-004", "Bong gân nhẹ",               "injury_first_aid", "otc_assist",    4)
    ,@("insect_sting",        "INJ-005", "Ong / kiến đốt",             "injury_first_aid", "otc_assist",    5)
    ,@("nosebleed_minor",     "INJ-006", "Chảy máu cam nhẹ",           "injury_first_aid", "otc_assist",    6)
    ,@("blister_foot",        "INJ-007", "Vết phồng rộp chân",         "injury_first_aid", "otc_assist",    7)
    ,@("severe_injury",       "INJ-008", "Chấn thương nặng",           "injury_first_aid", "capture_only",  8)

    # cardiovascular (5) - context_only / capture_only
    ,@("palpitations",        "CAR-001", "Tim đập nhanh / hồi hộp",    "cardiovascular", "capture_only",   1)
    ,@("leg_swelling",        "CAR-002", "Phù chân",                   "cardiovascular", "capture_only",   2)
    ,@("chest_pain",          "CAR-003", "Đau ngực",                   "cardiovascular", "capture_only",   3)
    ,@("high_bp_context",     "CAR-004", "Theo dõi huyết áp",          "cardiovascular", "context_only",   4)
    ,@("varicose_veins",      "CAR-005", "Giãn tĩnh mạch",             "cardiovascular", "context_only",   5)

    # other (1)
    ,@("other",               "OTH-001", "Khác / chưa rõ",             "other", "otc_assist",               1)
)

# symptom_code, alias
$Aliases = @(
    ,@("cough", "ho"), @("cough", "Ho"), @("cough", "ho khan nhe"), @("cough", "bi ho")
    ,@("cough_dry", "ho khan"), @("cough_dry", "Ho khan"), @("cough_dry", "ho k"), @("cough_dry", "khan tieng")
    ,@("cough_phlegm", "ho dom"), @("cough_phlegm", "ho co dom"), @("cough_phlegm", "Ho co dom"), @("cough_phlegm", "long dom")
    ,@("runny_nose", "so mui"), @("runny_nose", "sổ mũi"), @("runny_nose", "chay nuoc mui"), @("runny_nose", "chảy nước mũi")
    ,@("nasal_congestion", "nghet mui"), @("nasal_congestion", "nghẹt mũi"), @("nasal_congestion", "bit mui")
    ,@("sore_throat", "dau hong"), @("sore_throat", "đau họng"), @("sore_throat", "hong dau"), @("sore_throat", "viem hong")
    ,@("sneezing", "hat hoi"), @("sneezing", "hắt hơi"), @("sneezing", "bi hat hoi")
    ,@("common_cold", "cam lanh"), @("common_cold", "cảm lạnh"), @("common_cold", "cam cum"), @("common_cold", "cảm cúm"), @("common_cold", "bi cam")
    ,@("flu_symptoms", "cum"), @("flu_symptoms", "cúm"), @("flu_symptoms", "bi cum")
    ,@("shortness_of_breath", "kho tho"), @("shortness_of_breath", "khó thở"), @("shortness_of_breath", "tho kho"), @("shortness_of_breath", "ngop")
    ,@("wheezing", "tho kho khe"), @("wheezing", "thở khò khè"), @("wheezing", "kho khe")
    ,@("fever", "sot"), @("fever", "sốt"), @("fever", "bi sot"), @("fever", "sot cao"), @("fever", "ha sot")
    ,@("headache", "dau dau"), @("headache", "đau đầu"), @("headache", "nhuc dau"), @("headache", "dau dau du doi")
    ,@("body_ache", "dau nhuc"), @("body_ache", "đau nhức"), @("body_ache", "dau co"), @("body_ache", "met moi dau nguoi")
    ,@("chills", "ret run"), @("chills", "rét run"), @("chills", "lanh run")
    ,@("diarrhea", "tieu chay"), @("diarrhea", "tiêu chảy"), @("diarrhea", "di ngoai"), @("diarrhea", "di tung"), @("diarrhea", "tieu chay cap")
    ,@("nausea", "buon non"), @("nausea", "buồn nôn"), @("nausea", "muon non"), @("nausea", "non oi")
    ,@("vomiting", "non"), @("vomiting", "nôn"), @("vomiting", "oi"), @("vomiting", "non ra")
    ,@("abdominal_pain", "dau bung"), @("abdominal_pain", "đau bụng"), @("abdominal_pain", "bung dau"), @("abdominal_pain", "quac bung")
    ,@("heartburn", "o nong"), @("heartburn", "ợ nóng"), @("heartburn", "on lanh da day")
    ,@("reflux", "trao nguoc"), @("reflux", "trào ngược"), @("reflux", "o chua")
    ,@("constipation", "tao bon"), @("constipation", "táo bón"), @("constipation", "kho di")
    ,@("bloating", "day bung"), @("bloating", "đầy bụng"), @("bloating", "hoi bung")
    ,@("allergy", "di ung"), @("allergy", "dị ứng"), @("allergy", "bi di ung"), @("allergy", "mẩn ngứa")
    ,@("itchy_skin", "ngua da"), @("itchy_skin", "ngứa da"), @("itchy_skin", "bi ngua")
    ,@("rash", "phat ban"), @("rash", "phát ban"), @("rash", "noi man"), @("rash", "man do")
    ,@("hives", "me day"), @("hives", "mề đay"), @("hives", "noi me day")
    ,@("back_pain", "dau lung"), @("back_pain", "đau lưng"), @("back_pain", "lung dau")
    ,@("joint_pain", "dau khop"), @("joint_pain", "đau khớp"), @("joint_pain", "khop dau")
    ,@("toothache", "dau rang"), @("toothache", "đau răng"), @("toothache", "rang dau")
    ,@("ear_pain", "dau tai"), @("ear_pain", "đau tai"), @("ear_pain", "tai dau")
    ,@("red_eye", "do mat"), @("red_eye", "đỏ mắt"), @("red_eye", "mat do")
    ,@("itchy_eye", "ngua mat"), @("itchy_eye", "ngứa mắt")
    ,@("fatigue", "met moi"), @("fatigue", "mệt mỏi"), @("fatigue", "met"), @("fatigue", "kiet suc")
    ,@("dizziness", "chong mat"), @("dizziness", "chóng mặt"), @("dizziness", "hoa mat")
    ,@("insomnia", "mat ngu"), @("insomnia", "mất ngủ"), @("insomnia", "kho ngu"), @("insomnia", "khó ngủ")
    ,@("child_fever", "tre sot"), @("child_fever", "trẻ sốt"), @("child_fever", "be sot"), @("child_fever", "em sot")
    ,@("child_cough", "tre ho"), @("child_cough", "trẻ ho"), @("child_cough", "be ho")
    ,@("child_diarrhea", "tre tieu chay"), @("child_diarrhea", "trẻ tiêu chảy"), @("child_diarrhea", "be di ngoai")
)

# code, name_vi, severity, action, safety_level, message_vi, sort_order
$RiskFlags = @(
    ,@("difficult_breathing",    "Khó thở",              "emergency",  "refer_medical",    "refer_medical",    "Khó thở - không bán OTC; hướng cấp cứu / bác sĩ ngay.", 10)
    ,@("shortness_of_breath",    "Khó thở (triệu chứng)","urgent",     "refer_medical",    "refer_medical",    "Khó thở - không bán OTC; hướng cấp cứu / bác sĩ ngay.", 11)
    ,@("wheezing_severe",        "Thở khò khè nặng",     "urgent",     "refer_medical",    "refer_medical",    "Thở khò khè nặng - chuyển khám ngay.", 12)
    ,@("chest_pain",             "Đau ngực",             "emergency",  "refer_medical",    "refer_medical",    "Đau ngực - không bán OTC; hướng cấp cứu / bác sĩ ngay.", 20)
    ,@("loss_of_consciousness",  "Ngất / mất ý thức",    "emergency",  "stop_sale",        "stop_sale",        "Ngất / mất ý thức - dừng bán, gọi cấp cứu.", 30)
    ,@("unconscious",            "Ngất",                 "emergency",  "stop_sale",        "stop_sale",        "Ngất - dừng bán, gọi cấp cứu.", 31)
    ,@("seizure",                "Co giật",              "emergency",  "stop_sale",        "stop_sale",        "Co giật - dừng bán, gọi cấp cứu.", 32)
    ,@("severe_bleeding",        "Chảy máu nặng",        "emergency",  "refer_medical",    "refer_medical",    "Chảy máu nặng - không bán OTC; hướng cấp cứu.", 40)
    ,@("vomiting_blood",         "Nôn ra máu",           "emergency",  "refer_medical",    "refer_medical",    "Nôn ra máu - hướng cấp cứu / bác sĩ ngay.", 50)
    ,@("black_stool",            "Phân đen",             "urgent",     "refer_medical",    "refer_medical",    "Phân đen - cần khám để loại trừ chảy máu tiêu hóa.", 51)
    ,@("blood_in_stool",         "Đi ngoài ra máu",      "urgent",     "refer_medical",    "refer_medical",    "Đi ngoài ra máu - hướng khám ngay.", 52)
    ,@("severe_dehydration",     "Mất nước nặng",        "urgent",     "refer_medical",    "refer_medical",    "Mất nước nặng - bù nước và khám ngay.", 53)
    ,@("blood_in_urine",         "Tiểu máu",             "urgent",     "refer_medical",    "refer_medical",    "Tiểu máu - cần khám để loại trừ nhiễm trùng / bệnh lý.", 60)
    ,@("sudden_vision_loss",     "Mất thị lực đột ngột", "emergency",  "refer_medical",    "refer_medical",    "Mất thị lực đột ngột - hướng cấp cứu mắt ngay.", 70)
    ,@("infant_under_2",         "Trẻ dưới 2 tuổi",      "population", "refer_pharmacist", "refer_pharmacist", "Trẻ dưới 2 tuổi - không tự tư vấn OTC; chuyển dược sĩ.", 80)
    ,@("high_fever_infant",      "Trẻ nhỏ sốt cao",      "urgent",     "refer_medical",    "refer_medical",    "Trẻ nhỏ sốt cao - hướng khám ngay.", 81)
    ,@("pregnant",               "Mang thai",            "population", "refer_pharmacist", "refer_pharmacist", "Khách mang thai - chỉ dược sĩ tư vấn và quyết định sản phẩm.", 90)
    ,@("breastfeeding",          "Cho con bú",           "population", "refer_pharmacist", "refer_pharmacist", "Khách đang cho con bú - chỉ dược sĩ tư vấn.", 91)
    ,@("swallowing_difficulty",  "Khó nuốt",             "pharmacist", "refer_pharmacist", "refer_pharmacist", "Khó nuốt - chuyển dược sĩ, không tự chọn thuốc.", 100)
    ,@("severe_abdominal_pain",  "Đau bụng dữ dội",      "urgent",     "refer_medical",    "refer_medical",    "Đau bụng dữ dội - hướng khám ngay.", 110)
    ,@("anaphylaxis",            "Sốc phản vệ",          "emergency",  "stop_sale",        "stop_sale",        "Nghi sốc phản vệ - dừng bán, gọi cấp cứu.", 120)
    ,@("pregnancy_bleeding",     "Ra máu khi mang thai", "emergency",  "refer_medical",    "refer_medical",    "Ra máu khi mang thai - hướng khám thai sản ngay.", 130)
    ,@("stroke_symptoms",        "Dấu hiệu đột quỵ",     "emergency",  "stop_sale",        "stop_sale",        "Nghi đột quỵ - gọi cấp cứu ngay.", 140)
    ,@("drug_overdose",          "Quá liều thuốc",       "emergency",  "stop_sale",        "stop_sale",        "Nghi quá liều - gọi cấp cứu / chuyển bệnh viện.", 150)
    ,@("severe_head_injury",     "Chấn thương đầu nặng", "emergency",  "refer_medical",    "refer_medical",    "Chấn thương đầu nặng - hướng cấp cứu.", 160)
)

# code, question_vi, answer_type, sort_order
$Questions = @(
    ,@("Q_AGE",           "Tuổi / độ tuổi khách?",                    "number",        10)
    ,@("Q_DURATION",      "Triệu chứng kéo dài bao lâu?",             "duration_days", 20)
    ,@("Q_FEVER",         "Có sốt không?",                            "boolean",       30)
    ,@("Q_BREATHING",     "Có khó thở / thở khò khè không?",          "boolean",       40)
    ,@("Q_PREGNANCY",     "Có mang thai không?",                      "boolean",       50)
    ,@("Q_BREASTFEEDING", "Có đang cho con bú không?",                "boolean",       60)
    ,@("Q_SEVERITY",      "Mức độ nặng (1=nhẹ, 5=rất nặng)?",         "number",        70)
    ,@("Q_MEDICATION",    "Đang dùng thuốc gì?",                      "text",          80)
)

# symptom_code -> question codes (required)
$SymptomQuestions = @{
    "cough"    = @("Q_AGE", "Q_DURATION", "Q_FEVER", "Q_BREATHING", "Q_SEVERITY")
    "cough_dry" = @("Q_AGE", "Q_DURATION", "Q_FEVER", "Q_BREATHING", "Q_SEVERITY")
    "cough_phlegm" = @("Q_AGE", "Q_DURATION", "Q_FEVER", "Q_BREATHING", "Q_SEVERITY")
    "fever"    = @("Q_AGE", "Q_DURATION", "Q_SEVERITY", "Q_MEDICATION")
    "diarrhea" = @("Q_AGE", "Q_DURATION", "Q_FEVER", "Q_SEVERITY", "Q_MEDICATION")
}

# symptom_code -> risk_flag codes
$SymptomRiskRules = @(
    ,@("shortness_of_breath", "difficult_breathing", 10)
    ,@("shortness_of_breath", "shortness_of_breath", 20)
    ,@("wheezing",            "difficult_breathing", 10)
    ,@("wheezing",            "wheezing_severe",     20)
    ,@("breathing_discomfort","difficult_breathing", 10)
    ,@("chest_tightness",     "chest_pain",          10)
    ,@("child_wheezing",      "difficult_breathing", 10)
    ,@("blood_in_stool",      "blood_in_stool",      10)
    ,@("black_stool",         "black_stool",         10)
    ,@("vomiting_blood",      "vomiting_blood",      10)
    ,@("sudden_vision_loss",  "sudden_vision_loss",  10)
    ,@("blood_in_urine",      "blood_in_urine",      10)
    ,@("chest_pain",          "chest_pain",          10)
    ,@("pregnancy_bleeding",  "pregnancy_bleeding",  10)
    ,@("severe_allergy",      "anaphylaxis",         10)
    ,@("swallowing_discomfort","swallowing_difficulty", 10)
    ,@("child_dehydration",   "severe_dehydration",  10)
    ,@("dehydration_signs",   "severe_dehydration",  10)
)

# OTC knowledge rules - from ConsultationOtcRules patterns
$KnowledgeRules = @(
    @{ Symptom = "cough_dry";     RuleCode = "KR_COUGH_DRY";     Reason = "Ho khan - long đờm / giảm ho OTC"; Categories = @("HO_HAP"); Keywords = @("acc","acetylcysteine","prospan","ivy","ho khan","long dom","long đờm") }
    @{ Symptom = "cough_phlegm";  RuleCode = "KR_COUGH_PHLEGM";  Reason = "Ho đờm - long đờm OTC"; Categories = @("HO_HAP"); Keywords = @("acc","acetylcysteine","prospan","ivy","long dom","long đờm") }
    @{ Symptom = "cough";         RuleCode = "KR_COUGH";         Reason = "Ho - giảm ho / cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("prospan","decolgen","cam cum","ho") }
    @{ Symptom = "runny_nose";    RuleCode = "KR_RUNNY_NOSE";    Reason = "Sổ mũi - thuốc cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("decolgen","cam cum","so mui","sổ mũi","phenylephrine") }
    @{ Symptom = "nasal_congestion"; RuleCode = "KR_NASAL_CONG"; Reason = "Nghẹt mũi - thuốc cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("decolgen","cam cum","nghet mui","nghẹt mũi") }
    @{ Symptom = "sore_throat";   RuleCode = "KR_SORE_THROAT";   Reason = "Đau họng - giảm đau / cảm cúm OTC"; Categories = @("HO_HAP","GIAM_DAU"); Keywords = @("decolgen","cam cum","dau hong","đau họng","paracetamol") }
    @{ Symptom = "voice_hoarseness"; RuleCode = "KR_HOARSENESS"; Reason = "Khàn tiếng / ho — long đờm / giảm ho OTC"; Categories = @("HO_HAP","GIAM_DAU"); Keywords = @("prospan","acc","acetylcysteine","decolgen","cam cum","khan tieng","khàn tiếng","ho khan","dau hong","đau họng","long dom","long đờm") }
    @{ Symptom = "throat_clearing"; RuleCode = "KR_THROAT_CLEARING"; Reason = "Vướng họng / khản tiếng — giảm ho / cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("prospan","decolgen","cam cum","vuong hong","vướng họng","khan tieng","khàn tiếng","dau hong","đau họng") }
    @{ Symptom = "sneezing";      RuleCode = "KR_SNEEZING";      Reason = "Hắt hơi - cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("decolgen","cam cum","hat hoi","hắt hơi") }
    @{ Symptom = "common_cold";   RuleCode = "KR_COMMON_COLD";   Reason = "Cảm lạnh - thuốc cảm cúm OTC"; Categories = @("HO_HAP"); Keywords = @("decolgen","cam cum","cam lanh","cảm lạnh") }
    @{ Symptom = "flu_symptoms";  RuleCode = "KR_FLU";           Reason = "Cúm - thuốc cảm cúm / hạ sốt OTC"; Categories = @("HO_HAP","GIAM_DAU"); Keywords = @("decolgen","cam cum","paracetamol","cum","cúm") }
    @{ Symptom = "fever";         RuleCode = "KR_FEVER";         Reason = "Sốt - hạ sốt paracetamol OTC"; Categories = @("GIAM_DAU"); Keywords = @("paracetamol","panadol","efferalgan","tatanol","ha sot","hạ sốt") }
    @{ Symptom = "headache";      RuleCode = "KR_HEADACHE";      Reason = "Đau đầu - giảm đau OTC"; Categories = @("GIAM_DAU"); Keywords = @("paracetamol","panadol","efferalgan","ibuprofen","brufen","dau dau","đau đầu") }
    @{ Symptom = "body_ache";     RuleCode = "KR_BODY_ACHE";     Reason = "Đau nhức - giảm đau OTC"; Categories = @("GIAM_DAU","NGAO_DUOC"); Keywords = @("paracetamol","panadol","salonpas","dau nhuc","đau nhức") }
    @{ Symptom = "chills";        RuleCode = "KR_CHILLS";        Reason = "Rét run - hạ sốt / cảm cúm OTC"; Categories = @("GIAM_DAU","HO_HAP"); Keywords = @("paracetamol","decolgen","cam cum") }
    @{ Symptom = "diarrhea";      RuleCode = "KR_DIARRHEA";      Reason = "Tiêu chảy - men vi sinh / smecta OTC"; Categories = @("DA_DAY"); Keywords = @("smecta","diosmectite","gastropulgite","tieu chay","tiêu chảy") }
    @{ Symptom = "nausea";        RuleCode = "KR_NAUSEA";        Reason = "Buồn nôn - chống nôn OTC (cần DS nếu mang thai)"; Categories = @("DA_DAY"); Keywords = @("motilium","domperidone","buon non","buồn nôn") }
    @{ Symptom = "vomiting";      RuleCode = "KR_VOMITING";      Reason = "Nôn - chống nôn / bù nước OTC"; Categories = @("DA_DAY"); Keywords = @("motilium","domperidone","non","nôn","smecta") }
    @{ Symptom = "heartburn";     RuleCode = "KR_HEARTBURN";     Reason = "Ợ nóng - kháng acid OTC"; Categories = @("DA_DAY"); Keywords = @("omeprazole","antacid","o nong","ợ nóng") }
    @{ Symptom = "reflux";        RuleCode = "KR_REFLUX";        Reason = "Trào ngược - PPI OTC"; Categories = @("DA_DAY"); Keywords = @("omeprazole","trao nguoc","trào ngược") }
    @{ Symptom = "constipation";  RuleCode = "KR_CONSTIPATION";  Reason = "Táo bón - nhuận tràng OTC"; Categories = @("DA_DAY"); Keywords = @("constipation","tao bon","táo bón","fiber") }
    @{ Symptom = "bloating";      RuleCode = "KR_BLOATING";      Reason = "Đầy bụng - tiêu hóa OTC"; Categories = @("DA_DAY"); Keywords = @("simethicone","day bung","đầy bụng","motilium") }
    @{ Symptom = "abdominal_pain"; RuleCode = "KR_ABDOMINAL";    Reason = "Đau bụng - cần hỏi thêm / DS nếu dữ dội"; Categories = @("DA_DAY"); Keywords = @("smecta","dau bung","đau bụng") }
    @{ Symptom = "allergy";       RuleCode = "KR_ALLERGY";       Reason = "Dị ứng - kháng histamin OTC (hỏi tiền sử)"; Categories = @("VITAMIN"); Keywords = @("loratadine","cetirizine","clarityne","zyrtec","di ung","dị ứng") }
    @{ Symptom = "itchy_skin";    RuleCode = "KR_ITCHY_SKIN";    Reason = "Ngứa da - kháng histamin / bôi OTC"; Categories = @("VITAMIN","NGAO_DUOC"); Keywords = @("loratadine","cetirizine","ngua","ngứa") }
    @{ Symptom = "rash";          RuleCode = "KR_RASH";          Reason = "Phát ban - kháng histamin OTC"; Categories = @("VITAMIN"); Keywords = @("loratadine","cetirizine","phat ban","phát ban") }
    @{ Symptom = "hives";         RuleCode = "KR_HIVES";         Reason = "Mề đay - kháng histamin OTC"; Categories = @("VITAMIN"); Keywords = @("loratadine","cetirizine","me day","mề đay") }
    @{ Symptom = "back_pain";     RuleCode = "KR_BACK_PAIN";     Reason = "Đau lưng - giảm đau / dán OTC"; Categories = @("GIAM_DAU","NGAO_DUOC"); Keywords = @("paracetamol","salonpas","dau lung","đau lưng") }
    @{ Symptom = "joint_pain";    RuleCode = "KR_JOINT_PAIN";    Reason = "Đau khớp - giảm đau OTC"; Categories = @("GIAM_DAU","NGAO_DUOC"); Keywords = @("ibuprofen","brufen","salonpas","dau khop","đau khớp") }
    @{ Symptom = "toothache";     RuleCode = "KR_TOOTHACHE";     Reason = "Đau răng - giảm đau OTC (nên khám nha)"; Categories = @("GIAM_DAU"); Keywords = @("paracetamol","panadol","dau rang","đau răng") }
    @{ Symptom = "fatigue";       RuleCode = "KR_FATIGUE";       Reason = "Mệt mỏi - vitamin / bổ sung OTC"; Categories = @("VITAMIN"); Keywords = @("berocca","redoxon","vitamin","met moi","mệt mỏi") }
    @{ Symptom = "dizziness";     RuleCode = "KR_DIZZINESS";     Reason = "Chóng mặt - cần hỏi thêm / DS"; Categories = @("VITAMIN"); Keywords = @("berocca","vitamin","chong mat","chóng mặt") }
    @{ Symptom = "insomnia";      RuleCode = "KR_INSOMNIA";      Reason = "Mất ngủ - OTC an thần nhẹ (cần DS)"; Categories = @("VITAMIN"); Keywords = @("melatonin","mat ngu","mất ngủ") }
    @{ Symptom = "other";         RuleCode = "KR_OTHER";         Reason = "Triệu chứng khác - gợi ý OTC phổ biến"; Categories = @("VITAMIN","GIAM_DAU","HO_HAP"); Keywords = @() }
)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- KitPlatform 296: Pharmacy AI Assistant - Novixa Symptom Taxonomy V1 seed")
[void]$sb.AppendLine("-- Manifest: deploy/ubuntu/migration-files.prod.txt")
[void]$sb.AppendLine("-- Depends on: 295_pharmacy_symptom_taxonomy_schema.sql")
[void]$sb.AppendLine("-- Generated by: scripts/generate-pharmacy-symptom-seed.ps1")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("BEGIN;")
[void]$sb.AppendLine("")

# Categories
[void]$sb.AppendLine("-- pharmacy_symptom_category ($($Categories.Count) rows)")
$catLines = @()
foreach ($c in $Categories) {
    $catLines += "    ($(Escape-Sql $c.Code), $(Escape-Sql $c.LabelVi), $($c.Sort))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_symptom_category (code, label_vi, sort_order)")
[void]$sb.AppendLine("VALUES")
[void]$sb.AppendLine(($catLines -join ",`n"))
[void]$sb.AppendLine("ON CONFLICT (code) DO UPDATE SET")
[void]$sb.AppendLine("    label_vi = EXCLUDED.label_vi,")
[void]$sb.AppendLine("    sort_order = EXCLUDED.sort_order,")
[void]$sb.AppendLine("    updated_at = NOW();")
[void]$sb.AppendLine("")

# Symptoms
[void]$sb.AppendLine("-- pharmacy_symptom ($($Symptoms.Count) rows)")
$symLines = @()
foreach ($s in $Symptoms) {
    $id = Get-StableUuid "pharmacy_symptom" $s[0]
    $symLines += "    ('$id', $(Escape-Sql $s[0]), $(Escape-Sql $s[1]), $(Escape-Sql $s[2]), $(Escape-Sql $s[3]), $(Escape-Sql $s[4]), $($s[5]))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_symptom (id, code, taxonomy_ref, name_vi, category_code, consultation_mode, sort_order)")
[void]$sb.AppendLine("VALUES")
[void]$sb.AppendLine(($symLines -join ",`n"))
[void]$sb.AppendLine("ON CONFLICT (code) DO UPDATE SET")
[void]$sb.AppendLine("    taxonomy_ref = EXCLUDED.taxonomy_ref,")
[void]$sb.AppendLine("    name_vi = EXCLUDED.name_vi,")
[void]$sb.AppendLine("    category_code = EXCLUDED.category_code,")
[void]$sb.AppendLine("    consultation_mode = EXCLUDED.consultation_mode,")
[void]$sb.AppendLine("    sort_order = EXCLUDED.sort_order,")
[void]$sb.AppendLine("    updated_at = NOW();")
[void]$sb.AppendLine("")

# Aliases
[void]$sb.AppendLine("-- pharmacy_symptom_alias ($($Aliases.Count) rows)")
$aliasTuples = $Aliases | ForEach-Object {
    "    ($(Escape-Sql $_[0]), $(Escape-Sql $_[1]))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_symptom_alias (symptom_id, alias, source)")
[void]$sb.AppendLine("SELECT s.id, v.alias, 'novixa'")
[void]$sb.AppendLine("FROM (VALUES")
[void]$sb.AppendLine(($aliasTuples -join ",`n"))
[void]$sb.AppendLine(") AS v(code, alias)")
[void]$sb.AppendLine("JOIN pharmacy_symptom s ON s.code = v.code")
[void]$sb.AppendLine("ON CONFLICT (symptom_id, alias) DO NOTHING;")
[void]$sb.AppendLine("")

# Risk flags
[void]$sb.AppendLine("-- pharmacy_consultation_risk_flag ($($RiskFlags.Count) rows)")
$rfLines = @()
foreach ($r in $RiskFlags) {
    $id = Get-StableUuid "pharmacy_risk_flag" $r[0]
    $rfLines += "    ('$id', $(Escape-Sql $r[0]), $(Escape-Sql $r[1]), $(Escape-Sql $r[2]), $(Escape-Sql $r[3]), $(Escape-Sql $r[5]), $(Escape-Sql $r[4]), $($r[6]))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_consultation_risk_flag (id, code, name_vi, severity, action, message_vi, safety_level, sort_order)")
[void]$sb.AppendLine("VALUES")
[void]$sb.AppendLine(($rfLines -join ",`n"))
[void]$sb.AppendLine("ON CONFLICT (code) DO UPDATE SET")
[void]$sb.AppendLine("    name_vi = EXCLUDED.name_vi,")
[void]$sb.AppendLine("    severity = EXCLUDED.severity,")
[void]$sb.AppendLine("    action = EXCLUDED.action,")
[void]$sb.AppendLine("    message_vi = EXCLUDED.message_vi,")
[void]$sb.AppendLine("    safety_level = EXCLUDED.safety_level,")
[void]$sb.AppendLine("    sort_order = EXCLUDED.sort_order,")
[void]$sb.AppendLine("    updated_at = NOW();")
[void]$sb.AppendLine("")

# Questions
[void]$sb.AppendLine("-- pharmacy_consultation_question ($($Questions.Count) rows)")
$qLines = @()
foreach ($q in $Questions) {
    $id = Get-StableUuid "pharmacy_question" $q[0]
    $qLines += "    ('$id', $(Escape-Sql $q[0]), $(Escape-Sql $q[1]), $(Escape-Sql $q[2]), $($q[3]))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_consultation_question (id, code, question_vi, answer_type, sort_order)")
[void]$sb.AppendLine("VALUES")
[void]$sb.AppendLine(($qLines -join ",`n"))
[void]$sb.AppendLine("ON CONFLICT (code) DO UPDATE SET")
[void]$sb.AppendLine("    question_vi = EXCLUDED.question_vi,")
[void]$sb.AppendLine("    answer_type = EXCLUDED.answer_type,")
[void]$sb.AppendLine("    sort_order = EXCLUDED.sort_order,")
[void]$sb.AppendLine("    updated_at = NOW();")
[void]$sb.AppendLine("")

# Symptom risk rules - deactivate stale then upsert by stable id
[void]$sb.AppendLine("-- pharmacy_symptom_risk_rule")
[void]$sb.AppendLine("UPDATE pharmacy_symptom_risk_rule SET is_active = FALSE WHERE is_active = TRUE;")
$srLines = @()
foreach ($sr in $SymptomRiskRules) {
    $ruleId = Get-StableUuid "pharmacy_symptom_risk" "$($sr[0]):$($sr[1])"
    $srLines += "    ('$ruleId'::uuid, $(Escape-Sql $sr[0]), $(Escape-Sql $sr[1]), $($sr[2]))"
}
[void]$sb.AppendLine("INSERT INTO pharmacy_symptom_risk_rule (id, symptom_id, risk_flag_id, priority, is_active)")
[void]$sb.AppendLine("SELECT v.id, s.id, rf.id, v.priority, TRUE")
[void]$sb.AppendLine("FROM (VALUES")
[void]$sb.AppendLine(($srLines -join ",`n"))
[void]$sb.AppendLine(") AS v(id, symptom_code, risk_code, priority)")
[void]$sb.AppendLine("JOIN pharmacy_symptom s ON s.code = v.symptom_code")
[void]$sb.AppendLine("JOIN pharmacy_consultation_risk_flag rf ON rf.code = v.risk_code")
[void]$sb.AppendLine("ON CONFLICT (id) DO UPDATE SET")
[void]$sb.AppendLine("    symptom_id = EXCLUDED.symptom_id,")
[void]$sb.AppendLine("    risk_flag_id = EXCLUDED.risk_flag_id,")
[void]$sb.AppendLine("    priority = EXCLUDED.priority,")
[void]$sb.AppendLine("    is_active = TRUE,")
[void]$sb.AppendLine("    updated_at = NOW();")
[void]$sb.AppendLine("")

# Symptom question rules
[void]$sb.AppendLine("-- pharmacy_symptom_question_rule")
[void]$sb.AppendLine("DELETE FROM pharmacy_symptom_question_rule")
[void]$sb.AppendLine("WHERE symptom_id IN (SELECT id FROM pharmacy_symptom WHERE code IN ('cough','cough_dry','cough_phlegm','fever','diarrhea'));")
$sqTuples = @()
$prio = 10
foreach ($entry in $SymptomQuestions.GetEnumerator()) {
    foreach ($qCode in $entry.Value) {
        $sqTuples += "    ($(Escape-Sql $entry.Key), $(Escape-Sql $qCode), $prio)"
        $prio += 10
    }
}
[void]$sb.AppendLine("INSERT INTO pharmacy_symptom_question_rule (symptom_id, question_id, required, priority)")
[void]$sb.AppendLine("SELECT s.id, q.id, TRUE, v.priority")
[void]$sb.AppendLine("FROM (VALUES")
[void]$sb.AppendLine(($sqTuples -join ",`n"))
[void]$sb.AppendLine(") AS v(symptom_code, question_code, priority)")
[void]$sb.AppendLine("JOIN pharmacy_symptom s ON s.code = v.symptom_code")
[void]$sb.AppendLine("JOIN pharmacy_consultation_question q ON q.code = v.question_code")
[void]$sb.AppendLine("ON CONFLICT (symptom_id, question_id) DO UPDATE SET")
[void]$sb.AppendLine("    required = EXCLUDED.required,")
[void]$sb.AppendLine("    priority = EXCLUDED.priority,")
[void]$sb.AppendLine("    is_active = TRUE;")
[void]$sb.AppendLine("")

# Knowledge rules
[void]$sb.AppendLine("-- pharmacy_knowledge_rule ($($KnowledgeRules.Count) rows)")
$krLines = @()
$krPriority = 100
foreach ($kr in $KnowledgeRules) {
    $id = Get-StableUuid "pharmacy_knowledge_rule" $kr.RuleCode
    $krLines += "    ('$id'::uuid, $(Escape-Sql $kr.RuleCode), $(Escape-Sql $kr.Symptom), $(Escape-SqlArray $kr.Categories), $(Escape-SqlArray $kr.Keywords), $(Escape-Sql $kr.Reason), $krPriority)"
    $krPriority += 10
}
[void]$sb.AppendLine("INSERT INTO pharmacy_knowledge_rule (id, rule_code, symptom_id, category_codes, keywords, reason_vi, priority)")
[void]$sb.AppendLine("SELECT v.id::uuid, v.rule_code, s.id, v.category_codes, v.keywords, v.reason_vi, v.priority")
[void]$sb.AppendLine("FROM (VALUES")
[void]$sb.AppendLine(($krLines -join ",`n"))
[void]$sb.AppendLine(") AS v(id, rule_code, symptom_code, category_codes, keywords, reason_vi, priority)")
[void]$sb.AppendLine("JOIN pharmacy_symptom s ON s.code = v.symptom_code")
[void]$sb.AppendLine("ON CONFLICT (rule_code) DO UPDATE SET")
[void]$sb.AppendLine("    symptom_id = EXCLUDED.symptom_id,")
[void]$sb.AppendLine("    category_codes = EXCLUDED.category_codes,")
[void]$sb.AppendLine("    keywords = EXCLUDED.keywords,")
[void]$sb.AppendLine("    reason_vi = EXCLUDED.reason_vi,")
[void]$sb.AppendLine("    priority = EXCLUDED.priority,")
[void]$sb.AppendLine("    is_active = TRUE,")
[void]$sb.AppendLine("    updated_at = NOW();")

$content = $sb.ToString()

$content += @"

COMMIT;

INSERT INTO kit_schema_migrations (filename) VALUES ('296_pharmacy_symptom_taxonomy_seed.sql')
ON CONFLICT (filename) DO NOTHING;
"@

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$dir = Split-Path $resolvedOutput -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

[System.IO.File]::WriteAllText($resolvedOutput, $content, [System.Text.UTF8Encoding]::new($false))

$otcCount = ($Symptoms | Where-Object { $_[4] -eq 'otc_assist' }).Count
Write-Host "=== Pharmacy symptom seed generated ===" -ForegroundColor Cyan
Write-Host "Output: $resolvedOutput"
Write-Host "Categories: $($Categories.Count)"
Write-Host "Symptoms:   $($Symptoms.Count) (otc_assist: $otcCount)"
Write-Host "Aliases:    $($Aliases.Count)"
Write-Host "Risk flags: $($RiskFlags.Count)"
Write-Host "Questions:  $($Questions.Count)"
Write-Host "Knowledge:  $($KnowledgeRules.Count)"
