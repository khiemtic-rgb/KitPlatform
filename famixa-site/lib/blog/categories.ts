export type BlogCategoryId =
  | 'nuoi-day'
  | 'routine'
  | 'man-hinh'
  | 'tu-giac'
  | 'famixa';

export type BlogCategory = {
  id: BlogCategoryId;
  label: string;
  description: string;
};

export const BLOG_CATEGORIES: BlogCategory[] = [
  {
    id: 'nuoi-day',
    label: 'Nuôi dạy có phương pháp',
    description: 'Góc nhìn nuôi dạy con cân bằng, không áp lực.',
  },
  {
    id: 'routine',
    label: 'Nhịp sinh hoạt',
    description: 'Thói quen nhỏ giúp cả nhà đi cùng nhịp.',
  },
  {
    id: 'man-hinh',
    label: 'Màn hình & thiết bị',
    description: 'Thỏa thuận dùng máy thay vì cấm hay khóa app.',
  },
  {
    id: 'tu-giac',
    label: 'Tự giác & trưởng thành',
    description: 'Gợi ý giúp con tự làm, bố mẹ bớt nhắc.',
  },
  {
    id: 'famixa',
    label: 'Dùng Famixa',
    description: 'Tips thực tế khi dùng app với gia đình.',
  },
];

export function getBlogCategory(id: string) {
  return BLOG_CATEGORIES.find((category) => category.id === id);
}

export function getBlogCategoryLabel(id: string) {
  return getBlogCategory(id)?.label ?? id;
}
