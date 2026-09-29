import { readFileSync } from "node:fs";

let failed = 0;
function ok(cond: boolean, name: string) {
  if (!cond) {
    failed += 1;
    console.error("FAIL " + name);
  } else {
    console.log("ok " + name);
  }
}

const nav = readFileSync(new URL("./content-nav.tsx", import.meta.url), "utf8");
const router = readFileSync(new URL("../../app/router.tsx", import.meta.url), "utf8");
const api = readFileSync(new URL("../../shared/api/content.api.ts", import.meta.url), "utf8");
const factory = readFileSync(new URL("../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentGenerateService.cs", import.meta.url), "utf8");
const videoCtrl = readFileSync(new URL("../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs", import.meta.url), "utf8");
const seriesCtrl = readFileSync(new URL("../../../../../src/KitPlatform.Api/Controllers/Content/ContentArticleSeriesController.cs", import.meta.url), "utf8");
const mig = readFileSync(new URL("../../../../../migrations/370_pack_content_article_series.sql", import.meta.url), "utf8");
const manifest = readFileSync(new URL("../../../../../deploy/ubuntu/migration-files.content.txt", import.meta.url), "utf8");
const rules = readFileSync(new URL("../../../../../src/Packs/Content/KitPlatform.Packs.Content.Application/ContentArticleSeriesRules.cs", import.meta.url), "utf8");

ok(nav.includes("path: '/content/article-series'"), "nav article-series");
ok(!nav.includes("path: '/content/series'"), "nav does not smash video /content/series");
ok(nav.includes("label: 'Series'"), "nav label Series");
ok(router.includes("article-series/:seriesId/episodes/:episodeId"), "episode route");
ok(api.includes("/content/article-series"), "client api prefix");
ok(readFileSync(new URL("../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentArticleSeriesService.cs", import.meta.url), "utf8").includes("EnqueueGenerateTopicAsync"), "episode calls existing factory");
ok(factory.includes("EpisodeContextsAsync"), "factory reads series outline");
ok(videoCtrl.includes('[HttpGet("series/pilot")]'), "video series_pilot route kept");
ok(!videoCtrl.includes("article-series"), "video controller not rewritten");
ok(!videoCtrl.includes("story-series"), "no leftover story-series on ContentController");
ok(seriesCtrl.includes('[HttpGet("article-series")]'), "article series sibling controller");
ok(
  readFileSync(
    new URL("../../../../../src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentArticleSeriesService.cs", import.meta.url),
    "utf8",
  ).includes("episode.ContinuityJson, topicId, ct)"),
  "persist topic before enqueue",
);
ok(mig.includes("pack_content.content_series"), "mig series table");
ok(mig.includes("UNIQUE (series_id, episode_no)"), "unique episode no");
ok(!mig.includes("DROP TABLE") || !mig.toLowerCase().includes("series_pilot"), "no drop video tables");
ok(manifest.includes("370_pack_content_article_series.sql"), "content manifest");
ok(rules.includes("NOVIXA") || rules.includes("SeriesCode"), "codes");
ok(rules.includes("AllowReadingFromString") && rules.includes("FlexibleInt32JsonConverter"), "blueprint accepts string numbers");
ok(readFileSync(new URL("./ContentArticleSeriesPage.tsx", import.meta.url), "utf8").includes("export function ContentArticleSeriesPage"), "list page export");
ok(readFileSync(new URL("./ContentArticleSeriesPage.tsx", import.meta.url), "utf8").includes("corePackageId"), "list filter Core Idea");
ok(readFileSync(new URL("./ContentArticleSeriesPage.tsx", import.meta.url), "utf8").includes("DatePicker.RangePicker"), "list filter Date");
ok(readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8").includes('status: "PAUSED"'), "pause series action");
ok(
  readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8").includes(
    "contentTopicHref(ep.contentTopicId)",
  ),
  "series episode links topic",
);
ok(
  readFileSync(new URL("./ContentPackagesPage.tsx", import.meta.url), "utf8").includes(
    'to="/content/article-series"',
  ),
  "goc brand links Series",
);
ok(readFileSync(new URL("./content-lineage.tsx", import.meta.url), "utf8").includes("ContentLineageTrail"), "lineage trail");
ok(readFileSync(new URL("./ContentIdeaPoolPage.tsx", import.meta.url), "utf8").includes("ContentBrandOpportunityList"), "pool opportunities");
ok(readFileSync(new URL("./ContentPackagesPage.tsx", import.meta.url), "utf8").includes("PackageLineageBlock"), "goc lineage");
ok(readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8").includes("ContentSeriesNarrative"), "series narrative");
ok(readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("ContentEpisodeContinuity"), "episode continuity");
ok(readFileSync(new URL("./ContentTopicsPage.tsx", import.meta.url), "utf8").includes("useTopicLineageIndex"), "article lineage");
ok(nav.includes("CONTENT_NAV_OUTSIDE"), "videos outside narrative nav");
ok(nav.indexOf("label: 'Lịch tuần'") < nav.indexOf("label: 'Videos'"), "work group ends before videos");
ok(!readFileSync(new URL("./ContentOpsPage.tsx", import.meta.url), "utf8").includes("Idea Pool → Góc brand → duyệt"), "ops is not the narrative journey");
ok(readFileSync(new URL("./content-lineage.tsx", import.meta.url), "utf8").includes("Series content"), "series content label");
ok(readFileSync(new URL("./content-lineage.tsx", import.meta.url), "utf8").includes("Standalone"), "standalone content label");
ok(readFileSync(new URL("./content-lineage.tsx", import.meta.url), "utf8").includes("Bài độc lập"), "standalone trail");
ok(readFileSync(new URL("./ContentTopicsPage.tsx", import.meta.url), "utf8").includes("ContentKindTag"), "topics distinguish content kind");
ok(readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("seriesContentTrail"), "episode content lineage");
ok(readFileSync(new URL("./ContentArticleSeriesPage.tsx", import.meta.url), "utf8").includes("Brand Angle"), "series list brand angle");
ok(readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8").includes("Episode plan"), "series episode plan");
ok(readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8").includes("Quality Gate"), "series quality gate");
ok(readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("Tạo bài"), "create article from episode");
ok(readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("Xem bài"), "open existing article");
ok(readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("generateContentArticleEpisode"), "create uses factory");
ok(!readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8").includes("prompt"), "episode page sends no custom prompt");

const seriesDetail = readFileSync(new URL("./ContentArticleSeriesDetailPage.tsx", import.meta.url), "utf8");
const episodeDetail = readFileSync(new URL("./ContentArticleEpisodePage.tsx", import.meta.url), "utf8");
const lineage = readFileSync(new URL("./content-lineage.tsx", import.meta.url), "utf8");
ok(seriesDetail.includes("seriesDetailTrail"), "series header lineage");
ok(seriesDetail.includes('label: "Series code"'), "series header code");
ok(seriesDetail.includes('label: "Brand Angle"'), "series header angle");
ok(seriesDetail.includes('label: "Territory"'), "series header territory");
ok(seriesDetail.includes('label: "Narrative Direction"'), "series header direction");
ok(seriesDetail.includes('label: "CTA Strategy"'), "series header cta");
ok(seriesDetail.includes('label: "Continuity"'), "episode plan continuity");
ok(seriesDetail.includes("Mở Episode"), "episode plan opens episode");
ok(seriesDetail.includes("Xem bài"), "episode plan opens article");
const planBranch = seriesDetail.slice(seriesDetail.indexOf("ep.contentTopicId ?"));
ok(planBranch.includes("Xem bài") && planBranch.includes("Mở Episode"), "plan keeps both actions when content exists");
ok(episodeDetail.includes("episodeDetailTrail"), "episode lineage");
ok(episodeDetail.includes('label: "Sequence"') && episodeDetail.includes('label: "Key Message"'), "episode header");
ok(episodeDetail.includes("EpisodeCreativeBrief"), "creative brief on episode");
ok(episodeDetail.includes("ContentEpisodeContinuity"), "continuity block");
ok(lineage.includes("Previous Episode") && lineage.includes("Must Continue From"), "continuity previous and continue");
ok(lineage.includes("Must Not Repeat") && lineage.includes("Open Loop"), "continuity repeat and open loop");
ok(lineage.includes("Next Episode Direction"), "continuity next direction");
ok(lineage.includes("Core Point") && lineage.includes("Format"), "creative brief fields");
ok(episodeDetail.includes('title="Content"') && episodeDetail.includes('ContentKindTag kind="series"'), "content block");
ok(episodeDetail.includes('label: "Quality Gate"'), "episode quality gate");
const contentBranch = episodeDetail.slice(episodeDetail.lastIndexOf("ep.contentTopicId ?"));
const createAt = contentBranch.indexOf("Tạo bài");
ok(createAt > contentBranch.indexOf(") : ("), "create article only without content");
ok(!contentBranch.slice(0, contentBranch.indexOf(") : (")).includes("Tạo bài"), "existing content hides create");
ok(seriesDetail.includes("seriesHasEpisodeContent"), "series detects episode content");
ok(seriesDetail.includes("Series đã có nội dung. Canon được khóa để bảo vệ tính liên tục."), "series canon lock copy");
ok(seriesDetail.includes('disabled={canonLocked}'), "series canon actions disabled");
ok(seriesDetail.includes("Series đã có nội dung. Không tạo lại các Episode đã có bài."), "generate next lock copy");
ok(seriesDetail.includes('disabled={canonLocked}') && seriesDetail.includes("Generate next 10"), "generate next disabled with content");
ok(seriesDetail.includes("Thêm episode") && seriesDetail.includes('disabled={canonLocked}'), "add episode disabled with content");
ok(episodeDetail.includes("Episode đã được duyệt. Canon đã khóa."), "approved episode canon lock");
ok(episodeDetail.includes("contentStatusApproved"), "episode checks approved status");
ok(contentBranch.includes("Regenerate") && !contentBranch.slice(0, contentBranch.indexOf(") : (")).includes("Tạo bài"), "regenerate stays on episode");
const topics = readFileSync(new URL("./ContentTopicsPage.tsx", import.meta.url), "utf8");
ok(topics.includes("rewriteBlocked"), "content detail rewrite guard");
ok(topics.includes("if (lineageIndex.has(topicId)) return true;"), "series content blocks direct rewrite");
ok(topics.includes("return contentStatusApproved(status);"), "approved content blocks direct rewrite");
ok(topics.includes("Quay lại Episode"), "series content returns to episode");
ok(topics.includes("Regenerate từ Episode"), "regenerate goes through episode");
ok(topics.includes("generateContentArticleEpisode"), "episode regenerate uses episode flow");
ok(topics.includes("AI viết + ảnh"), "standalone keeps AI write action");
ok(readFileSync(new URL("./ContentPackagesPage.tsx", import.meta.url), "utf8").includes("packageRewriteBlocked"), "angle generate skips series content");

if (failed) {
  console.error(failed + " failed");
  process.exit(1);
}
console.log("content-article-series smoke ok");
