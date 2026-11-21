import { Container } from '@/components/ui/Container';
import { FilmPlay } from '@/components/sections/FilmPlay';
import { latestLongVideo } from '@/lib/youtube-latest';
import type { AppLocale } from '@/lib/cms/getLanding';

const HANDLE = 'famixavn215';

export async function LatestFilm({ locale }: { locale: AppLocale }) {
  const video = await latestLongVideo(HANDLE);
  if (!video) return null;
  const vi = locale === 'vi';

  return (
    <section id="phim" className="scroll-mt-24 bg-[#FBF8F1] pb-2 pt-1 md:pb-4">
      <Container>
        <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
          <div>
            <p className="m-0 text-[0.78rem] font-extrabold uppercase tracking-[0.08em] text-[#1f7a45]">
              {vi ? 'Phim gia đình' : 'Family film'}
            </p>
            <h2 className="m-0 mt-1 text-[1.45rem] font-extrabold text-[#08241C] md:text-[1.7rem]">
              {video.title}
            </h2>
          </div>
          <a
            href={video.channelUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="text-[0.92rem] font-bold text-[#1f7a45] hover:underline"
          >
            {vi ? 'Xem kênh Famixa' : 'Watch the Famixa channel'}
          </a>
        </div>
        <div className="overflow-hidden rounded-[24px] bg-[#08241C] shadow-[0_12px_40px_rgba(16,59,43,0.12)]">
          <FilmPlay videoId={video.id} title={video.title} />
        </div>
      </Container>
    </section>
  );
}
