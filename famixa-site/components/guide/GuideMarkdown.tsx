import type { ComponentPropsWithoutRef } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

type Props = {
  content: string;
};

function isImagePlaceholder(children: React.ReactNode) {
  return typeof children === 'string' && /^\[Ảnh minh họa(?:: .+)?\]$/.test(children.trim());
}

export function GuideMarkdown({ content }: Props) {
  return (
    <div className="guide-prose">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ href, children, ...props }: ComponentPropsWithoutRef<'a'>) => (
            <a href={href} {...props}>
              {children}
            </a>
          ),
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
