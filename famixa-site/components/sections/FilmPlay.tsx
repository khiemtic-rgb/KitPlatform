'use client';

import { useState } from 'react';

export function FilmPlay({ videoId, title }: { videoId: string; title: string }) {
  const [playing, setPlaying] = useState(false);

  if (playing) {
    return (
      <iframe
        className="aspect-video w-full"
        src={`https://www.youtube-nocookie.com/embed/${videoId}?autoplay=1`}
        title={title}
        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
        referrerPolicy="strict-origin-when-cross-origin"
        allowFullScreen
      />
    );
  }

  return (
    <button
      type="button"
      className="group relative block aspect-video w-full cursor-pointer border-0 bg-[#08241C] p-0"
      aria-label={title}
      onClick={() => setPlaying(true)}
    >
      <img
        src={`https://i.ytimg.com/vi/${videoId}/maxresdefault.jpg`}
        alt=""
        width={1280}
        height={720}
        className="h-full w-full object-cover"
        onError={(event) => {
          const img = event.currentTarget;
          const fallback = `https://i.ytimg.com/vi/${videoId}/hqdefault.jpg`;
          if (!img.src.endsWith('/hqdefault.jpg')) img.src = fallback;
        }}
      />
      <span className="absolute inset-0 grid place-items-center bg-black/10">
        <span className="grid h-16 w-16 place-items-center rounded-full bg-white text-[#103B2B] shadow-[0_8px_24px_rgba(0,0,0,0.28)] transition group-hover:scale-105">
          <svg viewBox="0 0 24 24" className="ml-1 h-7 w-7 fill-current" aria-hidden="true">
            <path d="M8 5.5v13l11-6.5-11-6.5Z" />
          </svg>
        </span>
      </span>
    </button>
  );
}
