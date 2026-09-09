export const ORCH_GATE = {
  VOICE_NOT_ASSIGNED: 'VOICE_NOT_ASSIGNED',
  MULTI_SPEAKER_LIPSYNC_UNSUPPORTED: 'MULTI_SPEAKER_LIPSYNC_UNSUPPORTED',
  DURATION_MISMATCH: 'DURATION_MISMATCH',
  PRODUCTION_SHORTER_THAN_DIALOGUE: 'PRODUCTION_SHORTER_THAN_DIALOGUE',
  PRODUCTION_LONGER_THAN_PERFORMANCE: 'PRODUCTION_LONGER_THAN_PERFORMANCE',
  PRODUCTION_BELOW_FLOOR: 'PRODUCTION_BELOW_FLOOR',
  NEEDS_VISUAL_REVIEW: 'NEEDS_VISUAL_REVIEW',
  LIPSYNC_QA_REQUIRED: 'LIPSYNC_QA_REQUIRED',
  CHARACTER_AUTHORITY_REQUIRED: 'CHARACTER_AUTHORITY_REQUIRED',
  VISUAL_MODE_AUTHORITY_REQUIRED: 'VISUAL_MODE_AUTHORITY_REQUIRED',
  DO_NOT_BLIND_RETRY: 'DO_NOT_BLIND_RETRY',
  CONFIRM_MOTION: 'CONFIRM_MOTION',
  CONFIRM_LIPSYNC: 'CONFIRM_LIPSYNC',
  STALE_VOICE: 'STALE_VOICE',
  STALE_KEYFRAME: 'STALE_KEYFRAME',
  STALE_MOTION: 'STALE_MOTION',
  STALE_LIPSYNC: 'STALE_LIPSYNC',
  STALE_MIX: 'STALE_MIX',
} as const;

export function staffOrchMessage(code: string) {
  if (code === ORCH_GATE.VOICE_NOT_ASSIGNED) return 'Chưa gán giọng cho nhân vật. Gán Voice rồi tạo lại.';
  if (code === ORCH_GATE.MULTI_SPEAKER_LIPSYNC_UNSUPPORTED) {
    return 'Cảnh này có nhiều người nói. Chưa hỗ trợ khớp môi nhiều nhân vật.';
  }
  if (code === ORCH_GATE.DURATION_MISMATCH) {
    return 'Thời lượng thoại dài hơn khả năng video hiện tại. Không cắt lời. Không kéo giọng.';
  }
  if (code === ORCH_GATE.PRODUCTION_SHORTER_THAN_DIALOGUE) {
    return 'Câu thoại kết thúc sau độ dài shot. Nới shot hoặc rút thoại — không cắt chữ.';
  }
  if (code === ORCH_GATE.PRODUCTION_LONGER_THAN_PERFORMANCE) {
    return 'Độ dài shot dài hơn take diễn. Không bịa thêm giây. Chọn take dài hơn hoặc rút shot.';
  }
  if (code === ORCH_GATE.PRODUCTION_BELOW_FLOOR) {
    return 'Shot ngắn hơn khoảng thoại + dư âm. Nới shot rồi mới ghép.';
  }
  if (code === ORCH_GATE.NEEDS_VISUAL_REVIEW) return 'Hình chưa đạt. Sửa hình trước khi tạo chuyển động.';
  if (code === ORCH_GATE.LIPSYNC_QA_REQUIRED) return 'Đánh dấu kiểm tra cảnh trước khi tạo Lip-sync.';
  if (code === ORCH_GATE.CHARACTER_AUTHORITY_REQUIRED) {
    return 'Đổi nhân vật phải qua Character Studio. Không sửa từ cảnh.';
  }
  if (code === ORCH_GATE.VISUAL_MODE_AUTHORITY_REQUIRED) {
    return 'Phong cách hình là của dự án. Không sửa từ cảnh.';
  }
  if (code === ORCH_GATE.DO_NOT_BLIND_RETRY) return 'Cùng lỗi trước đó. Đổi cách làm trước khi gửi lại.';
  if (code === ORCH_GATE.CONFIRM_MOTION) return 'Đang chờ xác nhận tạo video';
  if (code === ORCH_GATE.CONFIRM_LIPSYNC) return 'Đang chờ xác nhận Lip-sync';
  if (code === ORCH_GATE.STALE_VOICE) return 'Thoại cũ không còn khớp lời hiện tại.';
  if (code === ORCH_GATE.STALE_KEYFRAME) return 'Hình cũ không còn khớp hành động hiện tại.';
  if (code === ORCH_GATE.STALE_MOTION) return 'Video cũ không còn khớp hình hiện tại.';
  if (code === ORCH_GATE.STALE_LIPSYNC) return 'Lip-sync cũ không còn khớp thoại hiện tại.';
  if (code === ORCH_GATE.STALE_MIX) return 'Bản hoàn thiện cũ không còn khớp cảnh hiện tại.';
  return code;
}
