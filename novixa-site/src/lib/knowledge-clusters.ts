/** Ba cụm kiến thức khớp ba bài trên trang chủ. Bài không khớp từ khóa thì vào cụm theo chuyên mục. */

export const KNOWLEDGE_CLUSTERS = [
  {
    id: 'khach-quay-lai',
    title: 'Khách không quay lại',
    lead: 'Giữ khách đã mua.',
    leadSlug: 'mot-khach-quay-lai-nhieu-lan-dang-gia-hon-mot-khach-mua-mot-lan',
  },
  {
    id: 'hang-can-han',
    title: 'Hàng cận hạn',
    lead: 'Lô, hạn dùng và hàng phải hủy.',
    leadSlug: 'cach-giam-thuoc-can-han',
  },
  {
    id: 'chu-roi-quay',
    title: 'Chủ không rời được quầy',
    lead: 'Việc đang buộc chủ đứng quầy.',
    leadSlug: 'nha-thuoc-ban-kha-tot-nhung-chu-van-phai-co-mat-o-quay-ca-ngay',
  },
] as const;

export type KnowledgeClusterId = (typeof KNOWLEDGE_CLUSTERS)[number]['id'];

type ClusterPost = {
  id: string;
  data: { title: string; description?: string; category?: string };
};

function fold(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd')
    .toLowerCase();
}

export function clusterIdForPost(post: ClusterPost): KnowledgeClusterId {
  const hit = KNOWLEDGE_CLUSTERS.find((cluster) => cluster.leadSlug === post.id);
  if (hit) return hit.id;

  const text = fold(`${post.id} ${post.data.title} ${post.data.description ?? ''}`);
  if (/can han|het han|qua han|can date|fefo|han dung|ton kho|that thoat kho|kiem ke|nhap hang/.test(text)) {
    return 'hang-can-han';
  }
  if (/quay lai|tai mua|giu chan|cham soc khach|khach hang|khach cu|mot lan/.test(text)) {
    return 'khach-quay-lai';
  }
  if (/dung quay|roi quay|vang mat|co mat|chu nha thuoc|nhan vien|giao quay|goi chu/.test(text)) {
    return 'chu-roi-quay';
  }

  const category = post.data.category;
  if (category === 'ban-hang-cskh' || category === 'cau-chuyen-khach-hang' || category === 'connect') {
    return 'khach-quay-lai';
  }
  if (category === 'van-hanh') return 'hang-can-han';
  return 'chu-roi-quay';
}

export function groupKnowledgePosts<T extends ClusterPost>(posts: T[]) {
  const buckets = new Map<KnowledgeClusterId, T[]>(
    KNOWLEDGE_CLUSTERS.map((cluster) => [cluster.id, []]),
  );
  for (const post of posts) {
    buckets.get(clusterIdForPost(post))!.push(post);
  }
  return KNOWLEDGE_CLUSTERS.map((cluster) => {
    const items = buckets.get(cluster.id) ?? [];
    const lead = items.find((post) => post.id === cluster.leadSlug);
    const rest = items.filter((post) => post.id !== cluster.leadSlug);
    return { ...cluster, posts: lead ? [lead, ...rest] : rest };
  }).filter((group) => group.posts.length > 0);
}
