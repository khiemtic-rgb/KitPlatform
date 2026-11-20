import { http } from '@/shared/api/http';

export type AgentProposalStatus = 'Proposed' | 'Approved' | 'Posted' | 'Rejected' | 'Cancelled' | 'Failed';

export type AgentProposal = {
  id: string;
  brandId: string;
  topicId: string;
  title: string;
  targetKey: string;
  targetLabel: string;
  connectorType: string;
  siteId?: string | null;
  channelId?: string | null;
  publishAt: string;
  status: AgentProposalStatus;
  reason: string;
  publishJobId?: string | null;
  decidedBy?: string | null;
  decidedAt?: string | null;
  lastError?: string | null;
  createdAt: string;
  /** Kết quả thật của job đăng (Queued | Running | Succeeded | Failed | Cancelled); null với lượt đăng tay. */
  jobStatus?: string | null;
  jobError?: string | null;
  /** Link bài đã đăng nếu kênh trả về (YouTube, WordPress). */
  jobUrl?: string | null;
  jobUpdatedAt?: string | null;
  /** Chú thích TikTok đã duyệt, để chép khi mở bản nháp trên TikTok. */
  jobCaption?: string | null;
};

export type AgentSkipped = { topicId: string; title: string; reason: string };

export type AgentGenerateResult = {
  brandCode: string;
  created: number;
  superseded: number;
  proposals: AgentProposal[];
  skipped: AgentSkipped[];
  warnings: string[];
};

export async function generateAgentProposals(brand: string, days: number, maxPerDay: number) {
  const { data } = await http.post<AgentGenerateResult>('/content/agent/proposals/generate', null, {
    params: { brand, days, maxPerDay },
  });
  return data;
}

export async function fetchAgentProposals(params: { brand?: string; status?: string }) {
  const { data } = await http.get<AgentProposal[]>('/content/agent/proposals', { params });
  return data;
}

export async function approveAgentProposal(id: string, publishAt?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/approve`, {
    publishAt: publishAt ?? null,
  });
  return data;
}

export async function rejectAgentProposal(id: string, note?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/reject`, { note: note ?? null });
  return data;
}

/** Đăng lại lượt có job lỗi, kẹt hoặc còn chờ quá giờ: job về hàng chờ, đăng ở vòng quét kế tiếp (~15 giây). */
export async function retryAgentProposal(id: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/retry`, null);
  return data;
}

export async function cancelAgentProposal(id: string, note?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/cancel`, { note: note ?? null });
  return data;
}

/** Cài đặt lịch đăng của một thương hiệu: Agent dùng mỗi lần xếp lịch. */
export type AgentScheduleSettings = {
  localSlots: string[];
  minGapHours: number;
  maxPerDayPerChannel: number;
  shortPerDayPerChannel: number;
  longPerWeekPerChannel: number;
  days: number;
  /** Viết tự động theo Series (mặc định tắt). */
  autopilotEnabled: boolean;
  /** Số bài Agent viết nháp mỗi tuần. */
  autopilotPostsPerWeek: number;
  /** Tạm dừng đăng: bài đã có lịch đứng chờ, Agent không đăng cho tới khi bật lại. */
  publishPaused: boolean;
  /** Giữ file video trên máy chủ bao nhiêu ngày sau khi bài đã đăng xong mọi kênh. */
  mediaKeepDaysPosted: number;
  /** Giữ file video của bài đã huỷ hết lượt đăng bao nhiêu ngày. */
  mediaKeepDaysCancelled: number;
  /** false = chưa lưu, đang dùng mặc định. */
  saved: boolean;
};

export async function fetchAgentScheduleSettings(brandId: string) {
  const { data } = await http.get<AgentScheduleSettings>('/content/agent/schedule-settings', { params: { brandId } });
  return data;
}

export async function saveAgentScheduleSettings(brandId: string, body: Omit<AgentScheduleSettings, 'saved'>) {
  const { data } = await http.put<AgentScheduleSettings>('/content/agent/schedule-settings', body, { params: { brandId } });
  return data;
}

/** Dung lượng file video đính bài trên máy chủ (mọi thương hiệu). */
export type MediaStorage = {
  files: number;
  bytes: number;
  cleanableFiles: number;
  cleanableBytes: number;
  waitingFiles: number;
  waitingBytes: number;
  lastCleanupAt?: string | null;
  lastCleanupNote?: string | null;
};

export async function fetchMediaStorage() {
  const { data } = await http.get<MediaStorage>('/content/agent/media-storage', { timeout: 60_000 });
  return data;
}

export async function runMediaCleanup() {
  const { data } = await http.post<{ deletedFiles: number; freedBytes: number; notes: string[] }>(
    '/content/agent/media-cleanup',
    {},
    { timeout: 300_000 },
  );
  return data;
}

/** Bật/tắt "Tạm dừng đăng" của thương hiệu, lưu ngay; các cài đặt khác giữ nguyên. */
export async function setAgentPublishPaused(brandId: string, paused: boolean) {
  const { data } = await http.put<AgentScheduleSettings>(
    '/content/agent/schedule-settings',
    { publishPaused: paused },
    { params: { brandId } },
  );
  return data;
}

/** Kế hoạch đăng của một bài do Video Studio gửi: video dài/ngắn, loại kênh, thứ tự trong lô. */
export type TopicPublishPlanInput = {
  topicId: string;
  kind: 'long' | 'short';
  connectors: Array<'youtube_channel' | 'tiktok_channel' | 'facebook_page'>;
  seq: number;
  sourceRef?: string;
};

export type AgentScheduleTopicsResult = {
  scheduled: AgentProposal[];
  skipped: AgentSkipped[];
  warnings: string[];
};

/** Xếp các bài vào khung trống theo cài đặt lịch và duyệt luôn (người dùng đã duyệt ở Video Studio). */
export async function scheduleTopicsWithAgent(brand: string, topics: TopicPublishPlanInput[]) {
  const { data } = await http.post<AgentScheduleTopicsResult>('/content/agent/schedule-topics', { brand, topics }, { timeout: 120_000 });
  return data;
}

/** Lượt đăng tay: Fanpage chưa có API Meta nên duyệt xong không có job, người dùng tự đăng theo giờ. */
export function isManualProposal(p: AgentProposal) {
  return !p.publishJobId && (p.status === 'Approved' || p.status === 'Posted');
}

export async function markAgentProposalPosted(id: string, postUrl?: string) {
  const { data } = await http.post<AgentProposal>(`/content/agent/proposals/${id}/posted`, { note: postUrl ?? null });
  return data;
}

/** Kết quả một lượt Viết tự động: từng bước Agent đã làm hoặc lý do dừng. */
export type AutopilotReport = { brandId: string; brandCode: string; steps: string[]; wroteTopicId?: string | null };

/** Chạy ngay một lượt Viết tự động (bỏ giới hạn 1 bài/ngày; vẫn giữ hạn mức tuần và số bài chờ duyệt). */
export async function runAutopilotNow(brandId: string) {
  const { data } = await http.post<AutopilotReport>('/content/agent/autopilot/run', null, { params: { brandId }, timeout: 120_000 });
  return data;
}

export type PlatformPost = {
  id: string;
  brandId: string;
  channelId: string;
  channelName: string;
  connector: string;
  externalId: string;
  kind: string;
  title?: string | null;
  caption?: string | null;
  permalink?: string | null;
  status: string;
  publishAt?: string | null;
  matchedTopicId?: string | null;
  matchedTopicTitle?: string | null;
  syncedAt: string;
  sourceLabel: string;
};

export type PlatformSyncChannel = {
  channelName: string;
  connector: string;
  upserted: number;
  matched: number;
  unmatched: number;
  warning?: string | null;
};

export type PlatformSyncReport = {
  channels: number;
  upserted: number;
  matched: number;
  unmatched: number;
  byChannel: PlatformSyncChannel[];
  warnings: string[];
  migrationRequired: boolean;
};

/** Đọc lịch Fanpage và YouTube. Không sửa bài trên nền tảng. */
export async function syncPlatformPosts(brandId: string) {
  const { data } = await http.post<PlatformSyncReport>('/content/agent/platform-sync', null, {
    params: { brandId },
    timeout: 120_000,
  });
  return data;
}

export async function fetchPlatformPosts(brandId: string, unmatchedOnly = true) {
  const { data } = await http.get<PlatformPost[]>('/content/agent/platform-posts', {
    params: { brandId, unmatchedOnly },
  });
  return data;
}

export async function matchPlatformPost(id: string, topicId: string | null) {
  const { data } = await http.put<PlatformPost>(`/content/agent/platform-posts/${id}/match`, { topicId });
  return data;
}

/** Lịch hẹn trong Business Suite mà Graph không trả. Không đăng bài. */
export async function markPlatformOutside(body: {
  topicId: string;
  channelId: string;
  publishAt: string;
  kind?: string;
}) {
  const { data } = await http.post<PlatformPost>('/content/agent/platform-posts/manual', body);
  return data;
}
