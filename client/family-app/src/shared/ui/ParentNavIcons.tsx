import type { ReactNode } from 'react';

/** Lightweight SVG icons for parent chrome — brand teal, no emoji. */

type IconProps = {
  size?: number;
  className?: string;
  title?: string;
};

const stroke = {
  fill: 'none' as const,
  stroke: 'currentColor',
  strokeWidth: 1.9,
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
};

function Svg({ size = 22, className, title, children }: IconProps & { children: ReactNode }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      className={className}
      aria-hidden={title ? undefined : true}
      role={title ? 'img' : undefined}
    >
      {title ? <title>{title}</title> : null}
      {children}
    </svg>
  );
}

export function IconHome(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1v-9.5Z" />
    </Svg>
  );
}

export function IconTasks(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M9 6h11M9 12h11M9 18h11" />
      <path {...stroke} d="M5 6.2 6.2 7.5 8 5.2M5 12.2 6.2 13.5 8 11.2M5 18.2 6.2 19.5 8 17.2" />
    </Svg>
  );
}

export function IconReport(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M4 19V5M4 19h16" />
      <path {...stroke} d="M8 16v-5M12 16V8M16 16v-3" />
    </Svg>
  );
}

export function IconDiary(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M6 4h10a2 2 0 0 1 2 2v14l-3-2-3 2-3-2-3 2V6a2 2 0 0 1 2-2Z" />
      <path {...stroke} d="M9 8h6M9 12h6" />
    </Svg>
  );
}

export function IconBell(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M6.5 9.5a5.5 5.5 0 0 1 11 0c0 4.2 1.5 5.5 1.5 5.5H5s1.5-1.3 1.5-5.5Z" />
      <path {...stroke} d="M10 18.5a2 2 0 0 0 4 0" />
    </Svg>
  );
}

export function IconSettings(p: IconProps) {
  return (
    <Svg {...p}>
      <circle {...stroke} cx="12" cy="12" r="3.2" />
      <path
        {...stroke}
        d="M12 3.5v2.2M12 18.3v2.2M4.8 7.2l1.9 1.1M17.3 15.7l1.9 1.1M4.8 16.8l1.9-1.1M17.3 8.3l1.9-1.1"
      />
    </Svg>
  );
}

export function IconPlus(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M12 6v12M6 12h12" />
    </Svg>
  );
}

export function IconMembers(p: IconProps) {
  return (
    <Svg {...p}>
      <circle {...stroke} cx="9" cy="8" r="2.6" />
      <path {...stroke} d="M3.8 18.5c.6-3 2.6-4.5 5.2-4.5s4.6 1.5 5.2 4.5" />
      <circle {...stroke} cx="16.5" cy="8.5" r="2.2" />
      <path {...stroke} d="M14.2 14.2c1.6-.4 3.3.2 4.5 2.3" />
    </Svg>
  );
}

export function IconTarget(p: IconProps) {
  return (
    <Svg {...p}>
      <circle {...stroke} cx="12" cy="12" r="8" />
      <circle {...stroke} cx="12" cy="12" r="4.5" />
      <circle {...stroke} cx="12" cy="12" r="1.5" fill="currentColor" stroke="none" />
    </Svg>
  );
}

export function IconStar(p: IconProps) {
  return (
    <Svg {...p}>
      <path
        {...stroke}
        d="M12 3.8 14.2 9l5.5.5-4.2 3.6 1.3 5.3L12 15.8 7.2 18.4l1.3-5.3L4.3 9.5 9.8 9 12 3.8Z"
      />
    </Svg>
  );
}

export function IconTrophy(p: IconProps) {
  return (
    <Svg {...p}>
      <path {...stroke} d="M8 4h8v3a4 4 0 0 1-8 0V4Z" />
      <path {...stroke} d="M8 5H5.5a2.5 2.5 0 0 0 2.5 4M16 5h2.5A2.5 2.5 0 0 1 16 9" />
      <path {...stroke} d="M10 14h4v2.5H10zM9 19h6" />
    </Svg>
  );
}

export function IconRobot(p: IconProps) {
  return (
    <Svg {...p}>
      <rect {...stroke} x="5" y="8" width="14" height="11" rx="2.5" />
      <path {...stroke} d="M12 4v4M9.5 13h.1M14.5 13h.1M9 16.5h6" />
    </Svg>
  );
}
