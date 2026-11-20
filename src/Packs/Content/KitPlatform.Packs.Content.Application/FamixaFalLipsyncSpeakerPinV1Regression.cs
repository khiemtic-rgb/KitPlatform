namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncSpeakerPinV1Regression
{
    public const string SuiteId = FamixaFalLipsyncSpeakerPinV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var linh = FamixaFalLipsyncSpeakerPinV1.PixelCoordinates(0.66, 0.38, 1280, 720);
        Ok(linh.Length == 2 && linh[0] == 845 && linh[1] == 274, "T1 Linh right is integer pixels");
        Ok(linh[1] >= 0, "T2 no float leftover");

        var minh = FamixaFalLipsyncSpeakerPinV1.PixelCoordinates(0.28, 0.38, 1280, 720);
        Ok(minh[0] == 358 && minh[1] == 274, "T3 Minh left is integer pixels");

        var corner = FamixaFalLipsyncSpeakerPinV1.PixelCoordinates(1, 1, 1280, 720);
        Ok(corner[0] == 1279 && corner[1] == 719, "T4 clamp to last pixel");

        var fallback = FamixaFalLipsyncSpeakerPinV1.PixelCoordinates(0.5, 0.5);
        Ok(fallback[0] == 640 && fallback[1] == 360, "T5 default 1280x720");

        Ok(
            FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(0.66, 0.38, 1280, 720, 0, out var asdFrame, out var asdPin)
            && asdFrame == 0
            && asdPin[0] == 845
            && asdPin[1] == 274,
            "T6 Linh ASD is frame 0 + [845,274]");
        Ok(
            !FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(null, null, 1280, 720, 0, out _, out _),
            "T7 missing pin does not invent ASD");
        Ok(
            FamixaFalLipsyncSpeakerPinV1.UseAutoDetect(null, null, 1280, 720, 0),
            "T8 missing pin uses Fal auto_detect");
        Ok(
            !FamixaFalLipsyncSpeakerPinV1.UseAutoDetect(0.66, 0.38, 1280, 720, 0),
            "T8 valid pin is not auto");

        var linhFace = FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox(0.63, 0.25, 1280, 720);
        Ok(linhFace.Length == 4 && linhFace[0] == 726 && linhFace[1] == 90 && linhFace[2] == 886 && linhFace[3] == 270, "T9 Linh face box is right-side only");
        Ok(linhFace[0] > 500, "T9 Linh box does not cover Minh");
        Ok(FamixaFalLipsyncSpeakerPinV1.FrameCountOf(4) == 96, "T10 Take 55 4s@24fps is 96 boxes");
        Ok(FamixaFalLipsyncSpeakerPinV1.RepeatFaceBox(linhFace, 96).Length == 96, "T11 box list matches frame count");
        Ok(FamixaFalLipsyncSpeakerPinV1.FrameCountOf(5) == 120, "T12 shot clock 5s must not be sent when take is 96 frames");
        var take96 = VideStszMp4(96);
        Ok(KitVideoMotionRules.ReadMp4VideoSampleCount(take96) == 96, "T13 stsz video samples are 96");
        Ok(FamixaFalLipsyncSpeakerPinV1.FrameCountFromTake(take96, 5) == 96, "T13 take file wins over shot clock 5s");

        return fail;
    }

    private static byte[] VideStszMp4(int sampleCount)
    {
        static byte[] Be32(int n) => [(byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n];
        static byte[] Box(string type, byte[] payload)
        {
            var size = 8 + payload.Length;
            var box = new byte[size];
            Buffer.BlockCopy(Be32(size), 0, box, 0, 4);
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(type), 0, box, 4, 4);
            Buffer.BlockCopy(payload, 0, box, 8, payload.Length);
            return box;
        }

        var hdlr = Box("hdlr", [.. new byte[8], (byte)'v', (byte)'i', (byte)'d', (byte)'e', .. new byte[12]]);
        var stsz = Box("stsz", [.. new byte[8], .. Be32(sampleCount)]);
        var trak = Box("trak", [.. hdlr, .. stsz]);
        var moov = Box("moov", trak);
        var ftyp = Box("ftyp", [(byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0]);
        return [.. ftyp, .. moov];
    }
}
