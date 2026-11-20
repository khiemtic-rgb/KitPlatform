namespace KitPlatform.Packs.Content.Production;

/// <summary>Kiểm lõi thuần của M1 "Dựng tập từ bảng câu" (không DB, không FFmpeg).</summary>
public static class FamixaLinesheetV1Regression
{
    public const string SuiteId = "FAMIXA_LINESHEET_V1";

    const string Header = "id,scene,type,speaker,vi,en,direction,duration_ms,pause_after_ms,cue,screen_vi,screen_en";

    const string MiniScript =
        Header + "\r\n" +
        "T01_001,S01 A,music,,,,,2000,0,song_x,Đêm diễn (ghi chú),Show night\r\n" +
        "T01_002,S01 A,line,QUAN,\"Chào, cậu.\",\"Hi, you.\",nói nhanh,,300,,,\r\n" +
        "T01_003,S01 A,pause,,,,,3500,0,,,\r\n" +
        "T01_004,S02 B,line,MAI,Ừ.,Yeah.,gắt,,0,,,\r\n";

    const string MiniSettings = """
        {
          "scene_to_setting": { "S01 A": "stage", "S02 B": "kitchen" },
          "settings": {
            "stage":   { "label_vi": "Sân khấu", "palette": ["#111111", "#222222"], "cast": ["QUAN"] },
            "kitchen": { "label_vi": "Bếp",      "palette": ["#333333", "#444444"], "cast": ["MAI@0.4", "QUAN@0.7"] }
          },
          "characters": {
            "QUAN": { "name": "Quân", "color": "#3366cc", "height": 0.62 },
            "MAI":  { "name": "Mai",  "color": "#cc6633", "height": 0.58 }
          }
        }
        """;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        bool Throws(Action a, string? code = null)
        {
            try
            {
                a();
                return false;
            }
            catch (LinesheetValidationException ex)
            {
                return code is null || ex.Errors.Any(e => e.Code == code);
            }
            catch (InvalidOperationException ex)
            {
                return code is null || ex.Message.StartsWith(code, StringComparison.Ordinal);
            }
        }

        // ---- CSV ----
        var rows = LinesheetCsv.ParseScript(MiniScript);
        Ok(rows.Count == 4, "C1 parse 4 rows");
        Ok(rows[1].Vi == "Chào, cậu." && rows[1].PauseAfterMs == 300, "C2 quoted comma + pause");
        Ok(rows[0].IsMusic && rows[0].DurationMs == 2000 && rows[2].IsPause, "C3 types and durations");
        Ok(Throws(() => LinesheetCsv.ParseScript(MiniScript + "T01_004,S02 B,line,MAI,X,X,,,0,,,\r\n"), LinesheetErrorCodes.DuplicateId), "C4 duplicate id");
        Ok(Throws(() => LinesheetCsv.ParseScript(Header + "\r\nT01_001,S01 A,pause,,,,,,0,,,\r\n"), LinesheetErrorCodes.MissingDuration), "C5 pause needs duration");
        Ok(Throws(() => LinesheetCsv.ParseScript(Header + "\r\nT01_001,S01 A,line,QUAN,,,,,0,,,\r\n"), LinesheetErrorCodes.EmptyLine), "C6 empty line text");
        Ok(Throws(() => LinesheetCsv.ParseScript(Header + "\r\nT01_001,S01 A,dance,,,,,1,0,,,\r\n"), LinesheetErrorCodes.UnknownType), "C7 unknown type");
        Ok(Throws(() => LinesheetCsv.ParseScript("id,scene\r\nA,B\r\n"), LinesheetErrorCodes.MissingColumn), "C8 missing column");
        Ok(Throws(() => LinesheetCsv.ParseScript(Header + "\r\nT01_001,S01 A,pause,,,,,-5,0,,,\r\n"), LinesheetErrorCodes.BadNumber), "C9 negative number");

        // ---- dòng thời gian ----
        var durations = new Dictionary<string, int> { ["T01_002"] = 1200, ["T01_004"] = 500 };
        var tl = LinesheetTimelineBuilder.Build(rows, durations, allowMissing: false);
        Ok(tl.Entries[1].StartMs == 2000 && tl.Entries[1].EndMs == 3200, "T1 line after music");
        Ok(tl.Entries[2].StartMs == 3500 && tl.Entries[3].StartMs == 7000 && tl.TotalMs == 7500, "T2 pause_after then pause");
        Ok(Throws(() => LinesheetTimelineBuilder.Build(rows, new Dictionary<string, int>(), false), LinesheetErrorCodes.MissingAudio), "T3 missing audio blocks");
        var est = LinesheetTimelineBuilder.Build(rows, new Dictionary<string, int>(), true);
        Ok(est.EstimatedRowIds.SequenceEqual(["T01_002", "T01_004"]), "T4 estimate listed");
        Ok(LinesheetTimelineBuilder.SrtTime(3_723_045) == "01:02:03,045", "T5 srt time");
        var srt = LinesheetTimelineBuilder.Srt(rows, tl, LinesheetLanguages.En);
        Ok(srt.StartsWith("1\n00:00:02,000 --> 00:00:03,200\nHi, you.\n\n2\n", StringComparison.Ordinal), "T6 srt en");
        Ok(LinesheetTimelineBuilder.SrtRange(rows, tl, LinesheetLanguages.Vi, 0, 1500) == "", "T7 empty range srt is empty");
        Ok(LinesheetTimelineBuilder.FrameAt(2500, 24) == 60 && LinesheetTimelineBuilder.FrameAt(1062, 24) == 25, "T8 frame rounding");

        // ---- bảng shot ----
        var settings = FamixaLinesheetService.ParseSettings(MiniSettings);
        var shots = LinesheetShotPlanner.Draft(rows, tl, settings);
        Ok(shots.Count == 3, "S1 three shots (setting change + long pause)");
        Ok(shots[0].ShotId == "T01_SH001" && shots[0].Setting == "stage" && shots[0].TransitionIn == LinesheetTransitions.Fade, "S2 first shot fade");
        Ok(shots[0].Notes == LinesheetShotPlanner.PerformanceNote && shots[0].CameraPreset == LinesheetCameraPresets.Parallax, "S3 song → parallax + AI note");
        Ok(shots[1].StartRow == "T01_003" && shots[1].CameraPreset == LinesheetCameraPresets.SlowZoomIn, "S4 long pause alone, slow zoom");
        Ok(shots[2].Setting == "kitchen" && shots[2].CameraPreset == LinesheetCameraPresets.ShakeLight && shots[2].Focus == "MAI", "S5 tense → shake, focus speaker");
        Ok(LinesheetShotPlanner.ValidateShots(rows, shots, settings).Count == 0, "S6 draft validates");
        var roundTrip = LinesheetCsv.ParseShots(LinesheetCsv.WriteShots(shots));
        Ok(roundTrip.SequenceEqual(shots, ShotComparer.Instance), "S7 shots csv round trip");
        var gap = shots.Where((_, i) => i != 1).ToList();
        Ok(LinesheetShotPlanner.ValidateShots(rows, gap, settings).Any(e => e.Code == LinesheetErrorCodes.ShotGap), "S8 gap detected");
        var badPreset = shots.Select((s, i) => i == 0 ? s with { CameraPreset = "spin" } : s).ToList();
        Ok(LinesheetShotPlanner.ValidateShots(rows, badPreset, settings).Any(e => e.Code == LinesheetErrorCodes.ShotBadPreset), "S9 bad preset");
        Ok(Throws(() => LinesheetShotPlanner.Draft(rows, tl, FamixaLinesheetService.ParseSettings(MiniSettings.Replace("\"S02 B\": \"kitchen\"", "\"S09\": \"kitchen\""))),
            LinesheetErrorCodes.UnknownScene), "S10 unknown scene blocks draft");

        // ---- nét mặt (M1.1) ----
        var exprSettings = FamixaLinesheetService.ParseSettings(MiniSettings.TrimEnd().TrimEnd('}') + """
            , "expression_rules": [
                { "character": "QUAN", "expression": "hat", "cue_prefix": ["song_"] },
                { "character": "MAI", "expression": "gian", "direction_contains": ["GẮT"] } ] }
            """);
        var exprShots = LinesheetShotPlanner.Draft(rows, tl, exprSettings);
        Ok(exprShots[0].Expressions == "QUAN:hat", "E1 song cue → QUAN:hat");
        Ok(exprShots[2].Expressions == "MAI:gian", "E2 direction match ignores case");
        Ok(LinesheetShotPlanner.ExpressionFor(exprShots[2], "MAI") == "gian" && LinesheetShotPlanner.ExpressionFor(exprShots[2], "QUAN") == "", "E3 expression lookup");
        var exprCsv = LinesheetCsv.WriteShots(exprShots);
        Ok(exprCsv.StartsWith("shot_id,", StringComparison.Ordinal) && exprCsv.Split('\n')[0].TrimEnd('\r').EndsWith(",expressions", StringComparison.Ordinal)
           && LinesheetCsv.ParseShots(exprCsv)[2].Expressions == "MAI:gian", "E4 expressions column round trip");
        Ok(!LinesheetCsv.WriteShots(shots).Contains("expressions", StringComparison.Ordinal), "E5 no column when unused");
        var poseSettings = FamixaLinesheetService.ParseSettings(MiniSettings.TrimEnd().TrimEnd('}') + """
            , "expression_rules": [ { "character": "MAI", "expression": "ngoi", "settings": ["kitchen"] },
                                    { "character": "QUAN", "expression": "ngoi", "settings": ["kitchen"] } ] }
            """);
        var poseShots = LinesheetShotPlanner.Draft(rows, tl, poseSettings);
        Ok(poseShots[2].Expressions == "MAI:ngoi;QUAN:ngoi" && poseShots[0].Expressions == "", "E6 setting-only pose rule");

        // ---- giọng ElevenLabs ----
        var ttsSettings = FamixaLinesheetService.ParseSettings(MiniSettings.TrimEnd().TrimEnd('}') + """
            , "voices": { "QUAN": { "voice_id": "vq", "stability": 0.5 } } }
            """);
        var ttsItems = LinesheetTtsPlan.Plan(rows, ttsSettings, out var missingVoices);
        Ok(ttsItems.Count == 1 && ttsItems[0].RowId == "T01_002" && missingVoices.SequenceEqual(["MAI"]), "V1 plan per voice, missing listed");
        Ok(LinesheetTtsPlan.TagsFor(rows[3], ttsSettings) == "[angry]", "V2 direction gắt → [angry]");
        Ok(!LinesheetTtsPlan.ContainsWord("Trung tính", "run") && LinesheetTtsPlan.ContainsWord("Đáp trả, giọng run", "run"), "V3 whole-word match");
        Ok(LinesheetTtsPlan.SpokenText(rows[3] with { Vi = "Đỉnh thật!!  Mày ơi!" }, "[excited]") == "[excited] Đỉnh thật. Mày ơi.", "V4 no exclamation, tags first");
        Ok(LinesheetTtsPlan.TagsFor(rows[3] with { TtsTags = "[whispers]" }, ttsSettings) == "[whispers]", "V5 tts_tags column overrides");
        var fp1 = ttsItems[0].Fingerprint;
        ttsSettings.Voices["QUAN"].Speed = 0.9;
        Ok(LinesheetTtsPlan.Plan(rows, ttsSettings, out _)[0].Fingerprint != fp1, "V6 fingerprint changes with voice settings");
        Ok(LinesheetCsv.ParseScript(Header + ",tts_tags\r\nT01_001,S01 A,line,QUAN,Ừ.,Yeah.,,,0,,,,[softly]\r\n")[0].TtsTags == "[softly]", "V7 tts_tags column parsed");
        var take2 = LinesheetTtsPlan.Plan(rows, ttsSettings, out _, null, new Dictionary<string, int> { ["T01_002"] = 2 })[0];
        var take1 = LinesheetTtsPlan.Plan(rows, ttsSettings, out _)[0];
        Ok(take2.Take == 2 && take2.Fingerprint != take1.Fingerprint
           && take1.Fingerprint == LinesheetTtsPlan.Fingerprint(take1.VoiceId, ttsSettings.Voices["QUAN"], take1.Text), "V8 take 2 new fingerprint, take 1 unchanged");
        Ok(LinesheetTtsPlan.TagsFor(rows[3] with { Direction = "Bình thản giả, cười nhẹ" }, ttsSettings) == "[sad]"
           && LinesheetTtsPlan.TagsFor(rows[3] with { Direction = "Bất ngờ, buồn cười" }, ttsSettings) == "", "V9 bình thản giả → [sad], buồn cười không khớp");

        // ---- cân âm lượng ----
        Ok(LinesheetRenderPlan.ParseIntegratedLufs("-70.000000\n-24.1\n-17.2\n") == -17.2 && LinesheetRenderPlan.ParseIntegratedLufs("") is null, "L1 parse loudness");
        Ok(LinesheetRenderPlan.VoiceGainDb(-17.2, -20, 0) == -2.8 && LinesheetRenderPlan.VoiceGainDb(-40, -20, 0) == 12
           && LinesheetRenderPlan.VoiceGainDb(-17.2, 0, -2) == -2 && LinesheetRenderPlan.VoiceGainDb(null, -20, 1.5) == 1.5, "L2 gain clamp, off, voice gain");
        var mixA = LinesheetRenderPlan.MixTag(ttsSettings);
        ttsSettings.Voices["QUAN"].GainDb = -2;
        Ok(mixA != LinesheetRenderPlan.MixTag(ttsSettings), "L3 mix tag changes with gain_db");
        var gainArgs = LinesheetRenderPlan.GainArgs("in.wav", -2.8, "out.wav");
        Ok(gainArgs.Any(x => x.StartsWith("volume=-2.800dB", StringComparison.Ordinal)) && !gainArgs.Any(x => x.Contains("atrim") || x.Contains("loudnorm")), "L4 linear gain keeps length");
        Ok(FamixaLinesheetService.ParseSettings(MiniSettings).LoudnessLufs == -20, "L5 default loudness -20");
        var cueA = new LinesheetFileRecord { Kind = LinesheetFileKinds.CueSfx, FileKey = "door", Sha256 = "a" };
        var cueB = new LinesheetFileRecord { Kind = LinesheetFileKinds.CueSfx, FileKey = "door", Sha256 = "b" };
        var lineX = new LinesheetFileRecord { Kind = LinesheetFileKinds.LineAudio, FileKey = "T01_002", Sha256 = "x" };
        Ok(LinesheetRenderPlan.CueSetTag([cueA, lineX]) != LinesheetRenderPlan.CueSetTag([cueB, lineX])
           && LinesheetRenderPlan.CueSetTag([cueA, lineX]) == LinesheetRenderPlan.CueSetTag([lineX, cueA]), "L6 cue set tag changes with sfx file, ignores order");

        // ---- kế hoạch dựng ----
        var spans = LinesheetRenderPlan.Spans(rows, tl, shots, 24, 0, tl.TotalMs);
        Ok(spans.Sum(s => s.Frames) == LinesheetTimelineBuilder.FrameAt(tl.TotalMs, 24), "R1 frames sum = total");
        var clip = LinesheetRenderPlan.Spans(rows, tl, shots, 24, 2500, 6000);
        Ok(clip.Count == 2 && clip[0].StartMs == 2500 && clip[^1].EndMs == 6000, "R2 range clamps spans");
        var text = LinesheetRenderPlan.TextLayers(spans[0], tl, LinesheetLanguages.Vi);
        Ok(text.Count == 1 && text[0].Text == "Đêm diễn" && text[0].Kind == LinesheetTextKinds.Caption, "R3 screen text cleaned");
        var args = LinesheetRenderPlan.ShotArgs(spans[1], new LinesheetRenderPlan.ShotImages("flat.png", null, null), [], 1280, 720, 24, "o.mp4");
        Ok(args.Contains("-frames:v") && args[args.ToList().IndexOf("-frames:v") + 1] == spans[1].Frames.ToString(), "R4 exact frame count");
        Ok(!args.Any(a => a.Contains("Chào", StringComparison.Ordinal)), "R5 no dialogue text on command line");
        var mux = LinesheetRenderPlan.MuxArgs("v.mp4", "a.wav", 0, 7500, 180, 24, [], "o.mp4");
        Ok(!mux.Contains("-shortest") && !mux.Contains("-c:s") && mux.Contains("7.500"), "R6 mux -t, no empty subtitle track");
        var k1 = LinesheetRenderPlan.ShotCacheKey(spans[0], tl, 1280, 720, 24, "vi", ["a"]);
        Ok(k1 == LinesheetRenderPlan.ShotCacheKey(spans[0], tl, 1280, 720, 24, "vi", ["a"]), "R7 cache key stable");
        Ok(k1 != LinesheetRenderPlan.ShotCacheKey(spans[0], tl, 1280, 720, 24, "vi", ["b"]), "R8 cache key changes with asset");
        Ok(k1 != LinesheetRenderPlan.ShotCacheKey(spans[0], tl, 540, 960, 24, "vi", ["a"]), "R9 cache key changes with size");
        Ok(LinesheetRenderPlan.ConcatList([@"C:\x\it's.mp4"]) == "file 'C:/x/it'\\''s.mp4'\n", "R10 concat list quoting");

        // ---- chuyển động bằng video AI ----
        Ok(LinesheetMotionPlan.ClipSeconds(4000) == 5 && LinesheetMotionPlan.ClipSeconds(8000) == 10, "M1 clip seconds 5 or 10");
        var mPrompt = LinesheetMotionPlan.DefaultPrompt(spans[1]);
        Ok(mPrompt.Contains("Locked static camera") && !mPrompt.Contains("Chào"), "M2 default prompt english, no dialogue text");
        var mk = LinesheetMotionPlan.MotionKey(spans[1].Shot, ["a"], mPrompt, 10, "gen4_turbo");
        Ok(mk != LinesheetMotionPlan.MotionKey(spans[1].Shot with { Expressions = "QUAN:cuoi" }, ["a"], mPrompt, 10, "gen4_turbo")
           && mk != LinesheetMotionPlan.MotionKey(spans[1].Shot, ["b"], mPrompt, 10, "gen4_turbo"), "M3 motion key changes with expression and asset");
        var mArgs = LinesheetMotionPlan.MotionArgs(spans[1] with { Frames = 600 }, "m.mp4", 10, [], 1280, 720, 24, "o.mp4");
        var mGraph = mArgs[mArgs.ToList().IndexOf("-filter_complex") + 1];
        Ok(mGraph.Contains("reverse") && mArgs.Contains("600"), "M4 long shot ping-pongs, exact frames");
        var mSlow = LinesheetMotionPlan.MotionArgs(spans[1] with { Frames = 300 }, "m.mp4", 10, [], 1280, 720, 24, "o.mp4");
        Ok(mSlow[mSlow.ToList().IndexOf("-filter_complex") + 1].Contains("setpts=PTS*"), "M5 slightly long shot slows down");
        Ok(FamixaLinesheetService.Classify("pack/keyframes/kitchen_day_an_sang.png") == (LinesheetFileKinds.Character, "KF_kitchen_day_an_sang", ".png"), "K1 keyframe classified with prefix");
        var kfSettings = FamixaLinesheetService.ParseSettings(MiniSettings.TrimEnd().TrimEnd('}') + """
            , "keyframes": { "S01 A": "scene_a", "T01_SH001": "shot_one" } }
            """);
        Ok(LinesheetShotAssets.KeyframeName(spans[0].Shot with { ShotId = "T01_SH001" }, kfSettings) == "shot_one"
           && LinesheetShotAssets.KeyframeName(spans[0].Shot with { ShotId = "X", Scene = "S01 A" }, kfSettings) == "scene_a"
           && LinesheetShotAssets.KeyframeName(spans[0].Shot with { ShotId = "X", Scene = "Z" }, kfSettings) is null, "K2 keyframe by shot id, then scene");
        Ok(LinesheetMotionPlan.DefaultPrompt(spans[1]).Contains("nobody stands up") && LinesheetMotionPlan.ClipSeconds(8000, 5) == 5, "K3 lock rules, 5s clips");
        var face = LinesheetLipsyncPlan.FaceRect([775, 255, 180, 180], 1672, 941, 1280, 720);
        Ok(face.W == face.H && face.W % 2 == 0 && face.X > 520 && face.X < 580 && face.Y > 120 && face.Y < 170, "LS1 face box maps to cover-cropped frame: " + face);
        Ok(LinesheetLipsyncPlan.FaceRect([0, 0, 900, 900], 1672, 941, 1280, 720) is { X: >= 0, Y: >= 0 } edge && edge.X + edge.W <= 1280 && edge.Y + edge.H <= 720, "LS2 face box clamped inside frame");
        var lipGraph = LinesheetLipsyncPlan.OverlayGraph(new LinesheetPlacedLip("l.mp4", 24, 72, new LinesheetLipRect(100, 50, 200, 200)), 1, "v0", "lo0", 24);
        Ok(lipGraph.Contains("alphamerge") && lipGraph.Contains("overlay=x=100:y=50") && lipGraph.Contains("setpts=PTS+1.0000/TB") && lipGraph.Contains("trim=end_frame=48"), "LS3 lip overlay feathered, placed and timed");
        Ok(LinesheetLipsyncPlan.Scale(new LinesheetLipRect(100, 50, 200, 200), 1920, 1080) == new LinesheetLipRect(150, 76, 300, 300), "LS4 lip rect scales with render size");
        Ok(LinesheetMotionPlan.LockRules.Contains("Mouths stay gently closed"), "LS5 motion keeps mouths closed for lip sync");
        Ok(FamixaLinesheetService.ImageTag("ME", ["QUAN", "ME"]) == "ME_ref" && FamixaLinesheetService.ImageTag("ME2", ["QUAN", "ME"]) == "ME_ref2"
           && FamixaLinesheetService.ImageTag("QUAN", ["QUAN", "ME"]) == "QUAN" && FamixaLinesheetService.ImageTag("MEET", ["ME"]) == "MEET", "IG1 image tags are 3+ chars");

        // ---- tải file ----
        Ok(FamixaLinesheetService.Classify("audio/E01_002.wav") == (LinesheetFileKinds.LineAudio, "E01_002", ".wav"), "U1 audio");
        Ok(FamixaLinesheetService.Classify("pack/settings/stage/bg.png") == (LinesheetFileKinds.LayerBackground, "stage", ".png"), "U2 wrapped settings bg");
        Ok(FamixaLinesheetService.Classify("characters/QUAN.png") == (LinesheetFileKinds.Character, "QUAN", ".png"), "U3 character");
        Ok(FamixaLinesheetService.Classify("audio/../../etc/passwd.wav") is null, "U4 traversal rejected");
        Ok(FamixaLinesheetService.Classify("audio/x.exe") is null && FamixaLinesheetService.Classify("notes.txt") is null, "U5 unknown skipped");
        Ok(FamixaLinesheetService.Classify("settings/stage/top.png") is null, "U6 only bg/mid layers");
        Ok(FamixaLinesheetService.SafeName("a b/../c") == "abc", "U7 safe name");

        return fail;
    }

    sealed class ShotComparer : IEqualityComparer<LinesheetShot>
    {
        public static readonly ShotComparer Instance = new();

        public bool Equals(LinesheetShot? a, LinesheetShot? b) =>
            a is not null && b is not null && a with { Characters = [] } == b with { Characters = [] }
            && a.Characters.SequenceEqual(b.Characters);

        public int GetHashCode(LinesheetShot s) => s.ShotId.GetHashCode();
    }
}
