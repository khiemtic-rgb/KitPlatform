"""Đọc tiếng Việt bằng espeak-ng (giọng máy, chỉ để thử khớp miệng). Dùng: python tts_vi.py out.wav "câu 1" "câu 2" ..."""
import ctypes, sys, wave, numpy as np, espeakng_loader as E

lib = ctypes.CDLL(E.get_library_path())
rate = lib.espeak_Initialize(1, 0, E.get_data_path().encode(), 0)  # AUDIO_OUTPUT_RETRIEVAL
chunks = []
CB = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.POINTER(ctypes.c_short), ctypes.c_int, ctypes.c_void_p)
@CB
def cb(wav, n, ev):
    if n > 0:
        chunks.append(np.ctypeslib.as_array(wav, shape=(n,)).copy())
    return 0
lib.espeak_SetSynthCallback(cb)
lib.espeak_SetVoiceByName(b"vi")
lib.espeak_SetParameter(1, 150, 0)   # tốc độ (từ/phút): người già nói chậm
lib.espeak_SetParameter(3, 35, 0)    # cao độ thấp: giọng đàn ông
out, lines = sys.argv[1], sys.argv[2:]
pause = np.zeros(int(rate * 0.45), dtype=np.int16)
allpcm = [np.zeros(int(rate * 0.4), dtype=np.int16)]
for t in lines:
    chunks.clear()
    b = t.encode("utf-8")
    lib.espeak_Synth(b, len(b) + 1, 0, 1, 0, 1, None, None)   # POS_CHARACTER, espeakCHARS_UTF8
    lib.espeak_Synchronize()
    allpcm += chunks + [pause]
pcm = np.concatenate(allpcm)
with wave.open(out, "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate); w.writeframes(pcm.tobytes())
print(out, rate, "Hz", round(len(pcm) / rate, 2), "giây")
