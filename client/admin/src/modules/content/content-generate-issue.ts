export type ContentGenerateFix = {
  title: string;
  path: '/content/brands' | '/content/ai' | '/content/budget' | '/content/topics';
  tab?: 'brief' | 'images' | 'write';
};

/** Job nền Succeeded vẫn có thể là chặn trần — budgetBlocked không nằm trên WorkJob. */
export function isContentBudgetBlockMessage(raw?: string | null) {
  return /trần|ngân sách|BudgetBlocked|chặn gen/i.test(raw ?? '');
}

export function classifyContentGenerateIssue(raw: string): ContentGenerateFix | null {
  const err = (raw ?? '').trim();
  if (!err) return null;
  if (/Brand Brain|Kiến thức thương hiệu/i.test(err)) {
    return { title: 'Thiếu Brand Brain', path: '/content/brands' };
  }
  if (/chưa có nơi đăng|Thương hiệu chưa có nơi đăng/i.test(err)) {
    return { title: 'Chưa có nơi đăng', path: '/content/brands' };
  }
  if (/trần|ngân sách|BudgetBlocked|chặn gen/i.test(err)) {
    return { title: 'Chạm trần chi phí AI', path: '/content/budget' };
  }
  if (/Gemini|API key|Cấu hình AI|Gen ảnh|model ảnh|Không tạo được ảnh|ảnh lỗi/i.test(err)) {
    return { title: 'Cấu hình AI', path: '/content/ai' };
  }
  if (/thiếu bản Fanpage|Chưa có bản Fanpage/i.test(err)) {
    return { title: 'Thiếu bản Fanpage', path: '/content/topics', tab: 'write' };
  }
  return null;
}

export function classifyContentPublishIssue(raw: string): ContentGenerateFix | null {
  const err = (raw ?? '').trim();
  if (!err) return null;
  if (/Fanpage vẫn lỗi/i.test(err)) {
    return { title: 'Fanpage chưa kết nối', path: '/content/brands' };
  }
  if (/không đăng trùng|đã đăng rồi/i.test(err)) return null;
  if (/ảnh bìa|Chưa có ảnh|chỉ chữ|Đăng lại \+ ảnh/i.test(err)) {
    return { title: 'Thiếu ảnh bìa', path: '/content/topics', tab: 'images' };
  }
  if (/Quality gate|Creative Brief|Thiếu góc|web_long|chặn duyệt|chặn đăng/i.test(err)) {
    return { title: 'Quality gate chặn', path: '/content/topics', tab: 'brief' };
  }
  if (/bản Fanpage|fb_page/i.test(err)) {
    return { title: 'Thiếu bản Fanpage', path: '/content/topics', tab: 'write' };
  }
  if (/pages_manage|Kết nối lại|Facebook|Page Access Token/i.test(err)) {
    return { title: 'Fanpage chưa kết nối', path: '/content/brands' };
  }
  if (
    /GitHub|WordPress|applicationPassword|base_url|token không hợp lệ|Astro[/ ]?Git|owner \+ tên repo/i.test(
      err,
    )
  ) {
    return { title: 'Nơi đăng thiếu thông tin', path: '/content/brands' };
  }
  if (/Thái Nguyên Life|local_os|publisher|pack_local/i.test(err)) {
    return { title: 'Thái Nguyên Life chưa đăng được', path: '/content/brands' };
  }
  return classifyContentGenerateIssue(err);
}

export function generateFixOkText(path: ContentGenerateFix['path']) {
  if (path === '/content/ai') return 'Mở Model AI';
  if (path === '/content/budget') return 'Mở Chi phí AI';
  if (path === '/content/topics') return 'Mở bài viết';
  return 'Mở Thương hiệu';
}

export type ContentTopicTab = NonNullable<ContentGenerateFix['tab']> | 'write';

export function parseContentTopicTab(raw: string | null | undefined): ContentTopicTab | null {
  if (raw === 'brief' || raw === 'images' || raw === 'write') return raw;
  return null;
}

/** Same key as ContentFacebookCallbackPage — after chọn Page, quay lại bài/lịch. */
const FB_RETURN_STORAGE = 'kit.content.fbReturn';

export function rememberFacebookReturn(path: string) {
  try {
    sessionStorage.setItem(FB_RETURN_STORAGE, withFacebookPublishResume(path));
    sessionStorage.removeItem('kit.content.fbResumeLock');
  } catch {
    /* private mode */
  }
}

/** After chọn Page — mở lại bài và tự Xuất bản kênh còn thiếu (Fanpage). */
export function withFacebookPublishResume(path: string) {
  try {
    const u = new URL(path, 'http://local.invalid');
    if (u.pathname === '/content/topics' && u.searchParams.get('topic')) {
      u.searchParams.set('resumePublish', '1');
      return `${u.pathname}?${u.searchParams.toString()}`;
    }
  } catch {
    /* keep */
  }
  return path;
}

export function contentFacebookReconnectHref(brandId?: string | null, returnTo?: string | null) {
  if (returnTo) rememberFacebookReturn(returnTo);
  return contentBrandHref(brandId, { tab: 'targets', facebook: true });
}

export function contentBrandHref(
  brandId?: string | null,
  opts?: { tab?: 'targets'; facebook?: boolean },
) {
  const q = new URLSearchParams();
  if (brandId) q.set('brand', brandId);
  if (opts?.tab) q.set('tab', opts.tab);
  if (opts?.facebook) q.set('fb', '1');
  const s = q.toString();
  return s ? `/content/brands?${s}` : '/content/brands';
}

/** Deep-link Bài viết — Ops / Lịch / Góc brand dùng chung để mở đúng tab Brief hoặc Ảnh. */
export function contentTopicHref(topicId: string, tab?: ContentTopicTab | null) {
  const q = new URLSearchParams({ topic: topicId });
  if (tab === 'brief' || tab === 'images' || tab === 'write') q.set('tab', tab);
  return `/content/topics?${q.toString()}`;
}
