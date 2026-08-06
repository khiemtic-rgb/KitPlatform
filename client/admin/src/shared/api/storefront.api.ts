import { http } from '@/shared/api/http';

export type PharmacyStorefrontProfile = {
  slug: string;
  isPublished: boolean;
  publicHostHint: string;
  content: Record<string, unknown>;
  updatedAt: string;
};

export type UpdatePharmacyStorefrontProfileInput = {
  slug: string;
  isPublished: boolean;
  content: Record<string, unknown>;
};

function mapProfile(row: Record<string, unknown>): PharmacyStorefrontProfile {
  const contentRaw = row.content ?? row.Content ?? {};
  const content =
    typeof contentRaw === 'string'
      ? (JSON.parse(contentRaw) as Record<string, unknown>)
      : ((contentRaw as Record<string, unknown>) ?? {});

  return {
    slug: String(row.slug ?? row.Slug ?? ''),
    isPublished: Boolean(row.isPublished ?? row.IsPublished ?? false),
    publicHostHint: String(row.publicHostHint ?? row.PublicHostHint ?? ''),
    content,
    updatedAt: String(row.updatedAt ?? row.UpdatedAt ?? ''),
  };
}

export async function fetchPharmacyStorefrontProfile(): Promise<PharmacyStorefrontProfile> {
  const { data } = await http.get<Record<string, unknown>>('/pharmacy/storefront-profile');
  return mapProfile(data);
}

export async function updatePharmacyStorefrontProfile(
  input: UpdatePharmacyStorefrontProfileInput,
): Promise<PharmacyStorefrontProfile> {
  const { data } = await http.put<Record<string, unknown>>('/pharmacy/storefront-profile', {
    slug: input.slug,
    isPublished: input.isPublished,
    content: input.content,
  });
  return mapProfile(data);
}
