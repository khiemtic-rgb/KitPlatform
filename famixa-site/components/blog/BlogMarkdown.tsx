import type { ComponentPropsWithoutRef, ReactNode } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

type Props = {
  content: string;
};

function isImagePlaceholder(children: React.ReactNode) {
  return typeof children === 'string' && /^\[Ảnh minh họa(?:: .+)?\]$/.test(children.trim());
}

function flattenText(node: ReactNode): string {
  if (typeof node === 'string' || typeof node === 'number') return String(node);
  if (Array.isArray(node)) return node.map(flattenText).join('');
  return '';
}

/** Link app Famixa → nút CTA (markdown `[text](url)` hoặc URL trần). */
function isAppCtaLink(href?: string) {
  if (!href) return false;
  try {
    const host = new URL(href).hostname.toLowerCase();
    return host === 'home.famixa.vn' || host === 'app.famixa.vn';
  } catch {
    return /home\.famixa\.vn/i.test(href);
  }
}

function ctaLabel(href: string, children: ReactNode) {
  const text = flattenText(children).trim();
  if (!text || text === href || /^https?:\/\//i.test(text)) {
    return 'Khám phá Famixa ngay';
  }
  return children;
}

export function BlogMarkdown({ content }: Props) {
  return (
    <div className="guide-prose blog-prose">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ href, children, ...props }: ComponentPropsWithoutRef<'a'>) => {
            if (isAppCtaLink(href)) {
              return (
                <span className="blog-cta-wrap">
                  <a href={href} className="blog-cta-btn" {...props}>
                    {ctaLabel(href!, children)}
                    <span className="blog-cta-icon" aria-hidden>
                      →
                    </span>
                  </a>
                </span>
              );
            }
            return (
              <a href={href} {...props}>
                {children}
              </a>
            );
          },
          img: ({ src, alt, ...props }: ComponentPropsWithoutRef<'img'>) => (
            <img src={src} alt={alt ?? ''} loading="lazy" {...props} />
          ),
          p: ({ children, ...props }: ComponentPropsWithoutRef<'p'>) =>
            isImagePlaceholder(children) ? (
              <span className="guide-image-placeholder" role="note">
                {children}
              </span>
            ) : (
              <p {...props}>{children}</p>
            ),
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
