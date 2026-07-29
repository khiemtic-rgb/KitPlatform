import type { CmsImage } from '@/content/schema';

type Props = {
  image: CmsImage;
  className?: string;
  priority?: boolean;
  width?: number;
  height?: number;
};

/** CMS-ready image — swap src/alt from content JSON without touching sections */
export function CmsImage({ image, className = '', priority = false, width, height }: Props) {
  return (
    <img
      src={image.src}
      alt={image.alt}
      width={width ?? image.width ?? 800}
      height={height ?? image.height ?? 600}
      className={className}
      loading={priority ? 'eager' : 'lazy'}
      decoding="async"
    />
  );
}
