/** Video pipeline routing. Run: npx tsx src/modules/content/video-pipeline.smoke.ts */
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  VIDEO_PIPELINE_WORKFLOWS,
  createPipelineChoice,
  offeredForNewFilm,
  resolveVideoWorkspace,
  retargetRefusal,
  seriesCodeForPipeline,
  characterDeskForFilm,
  filmScriptSurfaces,
  reopenFilmSurface,
  opensNovixaMotion,
  usesNovixaLocalVideo,
  videoSeriesLine,
  videoWorkflowDescription,
  videoWorkflowLabel,
} from './video-pipeline';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const series = '01a17364-4836-707c-bd63-414942c753c9';
const short = '01a17364-4836-707c-bd63-414942c753ca';

ok(resolveVideoWorkspace({ filmTypeId: series, workflowKey: 'famixa_series', seriesCode: 'NOVIXA' }) === 'series_continuity', 'series workflow opens series workspace');
ok(resolveVideoWorkspace({ filmTypeId: short, workflowKey: 'factory_short', seriesCode: 'NOVIXA' }) === 'short_factory', 'short workflow opens short workspace');
ok(resolveVideoWorkspace({ filmTypeId: short, workflowKey: 'guided_production', seriesCode: 'VAN_DINH' }) === 'guided', 'guided workflow opens guided workspace');
ok(resolveVideoWorkspace({ filmTypeId: null, workflowKey: null, seriesCode: 'NOVIXA' }) === 'series_continuity', 'an existing film opens on its own series code');
ok(resolveVideoWorkspace({ filmTypeId: null, workflowKey: 'famixa_series', seriesCode: '' }) === 'unconfigured', 'a workflow key without a film does not choose a workspace');
ok(resolveVideoWorkspace({ filmTypeId: null, seriesCode: 'FAMIXA' }) === 'series_continuity', 'legacy catalog keeps series workspace');
ok(offeredForNewFilm('active') && !offeredForNewFilm('archived') && !offeredForNewFilm('draft'), 'disabled pipeline is hidden from create');
const choiceNone = createPipelineChoice([{ status: 'archived' }, { status: 'draft' }]);
ok(choiceNone.active.length === 0 && !choiceNone.selected, 'no active pipeline is not auto-selected');
const choiceOne = createPipelineChoice([{ status: 'archived', id: 'off' }, { status: 'active', id: 'only' }]);
ok(choiceOne.selected?.id === 'only', 'one active pipeline is selected');
const choiceMany = createPipelineChoice([{ status: 'active', id: 'a' }, { status: 'active', id: 'b' }]);
ok(choiceMany.active.length === 2 && !choiceMany.selected, 'two pipelines wait for an explicit choice');
const choiceDefault = createPipelineChoice([
  { status: 'active', id: 'a', isDefault: false },
  { status: 'active', id: 'b', isDefault: true },
  { status: 'archived', id: 'c', isDefault: true },
]);
ok(choiceDefault.selected?.id === 'b', 'the active default is offered when a brand has several pipelines');
ok(retargetRefusal(series, short) !== null, 'nav cannot swap the stored pipeline');
ok(retargetRefusal(series, series) === null, 'the same pipeline is not a retarget');
ok(videoSeriesLine({ filmTypeId: short, seriesCode: 'NOVIXA', seriesLabel: '', pipelineName: 'Nhà thuốc' }) === 'Nhà thuốc', 'empty label uses the pipeline name');
ok(!(videoSeriesLine({ filmTypeId: short, seriesCode: 'NOVIXA', seriesLabel: '', pipelineName: 'Nhà thuốc' }) || '').includes('Video giáo dục'), 'short pipeline does not inherit the series label');
ok(videoSeriesLine({ filmTypeId: null, seriesCode: 'FAMIXA', seriesLabel: '', pipelineName: 'Famixa' }) === undefined, 'legacy catalog keeps its own chrome');
ok(videoSeriesLine({ filmTypeId: null, seriesCode: 'NOVIXA', seriesLabel: '', pipelineName: '' }) === 'NOVIXA', 'an unbound film keeps its own series name');
ok(!(videoSeriesLine({ filmTypeId: null, seriesCode: 'NOVIXA', seriesLabel: '', pipelineName: '' }) || '').includes('Video giáo dục'), 'an unbound Novixa film does not use the education label');
ok(
  VIDEO_PIPELINE_WORKFLOWS.map((row) => row.value).join(',') === 'famixa_series,factory_short,guided_production',
  'workflow codes stay unchanged',
);
ok(videoWorkflowLabel('famixa_series') === 'Phim theo series', 'series workflow name');
ok(videoWorkflowLabel('factory_short') === 'Video ngắn độc lập', 'short workflow name');
ok(videoWorkflowLabel('guided_production') === 'Sản xuất phim theo kịch bản', 'scripted workflow name');
ok(
  videoWorkflowDescription('guided_production').includes('kịch bản') &&
    !VIDEO_PIPELINE_WORKFLOWS.some((row) => row.label.includes('kiểm duyệt') || row.label === 'Series nhiều tập' || row.label === 'Video ngắn'),
  'old workflow labels are gone',
);
ok(seriesCodeForPipeline('novixa-ops') === 'NOVIXA_OPS', 'pipeline code normalizes');
ok(seriesCodeForPipeline('!!!') === null, 'invalid pipeline code is not replaced');

const root = dirname(fileURLToPath(import.meta.url));
const host = readFileSync(join(root, 'ContentVideoPipelineHost.tsx'), 'utf8');
const studio = readFileSync(join(root, 'ContentStudioPage.tsx'), 'utf8');
const route = readFileSync(join(root, 'ContentVideosRoute.tsx'), 'utf8');
const settings = readFileSync(join(root, 'ContentFilmTypesPage.tsx'), 'utf8');
ok(filmScriptSurfaces('factory_short').join(',') === 'film', 'factory_short has one script surface');
ok(filmScriptSurfaces('guided_production').join(',') === 'film', 'guided_production has one script surface');
ok(filmScriptSurfaces('famixa_series').join(',') === 'story', 'famixa_series keeps the series story desk');
const novixaFactory = reopenFilmSurface({
  filmTypeId: '01a17637-7bfd-7fa8-ad20-4f75f392114f',
  workflowKey: 'factory_short',
  seriesCode: 'NOVIXA',
});
ok(novixaFactory.script === 'film' && novixaFactory.autoOpenShot === 'none', 'Novixa factory_short reopened by build stays on the film desk');
ok(
  reopenFilmSurface({ filmTypeId: '01a17637-7bfd-7fa8-ad20-4f75f392114f', workflowKey: 'guided_production', seriesCode: 'NOVIXA' }).script === 'film',
  'Novixa guided_production reopened by build uses the film desk',
);
ok(
  reopenFilmSurface({ filmTypeId: null, workflowKey: null, seriesCode: 'NOVIXA' }).script === 'story' &&
    reopenFilmSurface({ filmTypeId: series, workflowKey: 'famixa_series', seriesCode: 'FAMIXA' }).script === 'story',
  'famixa and a film with no pipeline keep the series story desk',
);
ok(
  reopenFilmSurface({ filmTypeId: novixaFactory.workflowKey, workflowKey: 'factory_short', seriesCode: 'NOVIXA', shot: 'EP01-SC01-SH01' }).autoOpenShot === 'url',
  'an explicit shot query still opens that shot',
);
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const workspaceFile = readFileSync(join(root, 'ContentFamixaSeriesWorkspace.tsx'), 'utf8');
ok(tab.includes('usesFilmScriptDesk(workflowKey)') && tab.includes('filmLed ? ('), 'a short or guided film mounts the film desk');
ok(tab.includes('reopenFilmSurface') && tab.includes("opened.autoOpenShot !== 'first'"), 'reopening a film desk does not jump into the first shot');
ok(tab.includes('filmLed ? null : prodMode'), 'a short or guided film does not mount the story form');
ok(tab.includes('ContentFamixaDirectorStoryDesk') && tab.includes('ContentFamixaSeriesWorkspace'), 'famixa series still has the series desk');
ok(tab.includes('ContentFamixaShotProductionPanel') && tab.includes('onOpenShot'), 'a shot still opens its own workspace');
ok(workspaceFile.includes('hideStoryForm') && workspaceFile.includes("item.id === 'script'"), 'the story tab is hidden when the film desk owns the script');
ok(host.includes("workspace === 'short_factory'") && host.includes('workflowKey={workflowKey || undefined}'), 'short and guided films follow the stored workflow');
ok(host.includes('resolveVideoWorkspace'), 'host routes from the stored film');
ok(!host.includes('useContentBrand'), 'host does not read the nav brand');
ok(!host.includes('FAMIXA'), 'host does not name the legacy catalog');
ok(studio.includes('filmTypeId'), 'studio stores the chosen pipeline');
ok(studio.includes('Chưa có Video Pipeline'), 'studio explains a missing pipeline');
const api = readFileSync(join(root, '../../shared/api/content.api.ts'), 'utf8');
ok(api.includes('brandCode') && !api.includes('brandId ? { brandId } : { brandCode }'), 'a chosen brand is not listed from the legacy catalog');
ok(route.includes("get('build')") && route.includes('ContentVideoPipelineHost'), 'an open film uses the stored pipeline host');
ok(settings.includes('updateFilmType') && settings.includes('Pipeline của brand này'), 'brand settings edit and isolate pipelines');
ok(usesNovixaLocalVideo({ filmTypeId: '01a17637-7bfd-7fa8-ad20-4f75f392114f', workflowKey: 'guided_production', brandCode: 'novixa' }), 'a stored Novixa guided film uses the local video path');
ok(usesNovixaLocalVideo({ filmTypeId: short, workflowKey: 'factory_short', brandCode: 'NOVIXA' }), 'a stored Novixa short film uses the local video path');
ok(!usesNovixaLocalVideo({ filmTypeId: null, workflowKey: null, brandCode: 'novixa' }), 'an unbound film does not inherit the Novixa renderer');
ok(!usesNovixaLocalVideo({ filmTypeId: series, workflowKey: 'famixa_series', brandCode: 'novixa' }), 'famixa_series does not use the Novixa renderer');
ok(!usesNovixaLocalVideo({ filmTypeId: short, workflowKey: 'guided_production', brandCode: 'famixa' }), 'another brand does not use the Novixa renderer');
ok(opensNovixaMotion({ seriesCode: 'NOVIXA' }), 'a Novixa series film opens the motion desk');
ok(!opensNovixaMotion({ seriesCode: 'NOVIXA', workflowKey: 'famixa_series', filmTypeId: series }), 'a film stored as famixa_series stays on that desk');
ok(!opensNovixaMotion({ seriesCode: 'FAMIXA' }), 'the Famixa catalog keeps its own desk');
ok(host.includes('opensNovixaMotion') && host.includes('ContentNovixaLocalStudio'), 'the host opens the Novixa studio from the stored film');
ok(!host.includes('useContentBrand'), 'host does not read the nav brand');
ok(characterDeskForFilm({ workflowKey: 'famixa_series', seriesCode: 'FAMIXA' }) === 'famixa', 'a famixa_series film keeps Character Studio');
ok(characterDeskForFilm({ seriesCode: 'FAMIXA' }) === 'famixa', 'the unbound Famixa catalog keeps its character set');
ok(characterDeskForFilm({ workflowKey: 'factory_short', seriesCode: 'NOVIXA' }) === 'unavailable', 'factory_short does not take the Famixa character set');
ok(characterDeskForFilm({ workflowKey: 'guided_production', seriesCode: 'NOVIXA' }) === 'unavailable', 'guided_production does not take the Famixa character set');
ok(characterDeskForFilm({ seriesCode: 'NOVIXA' }) === 'unconfigured', 'an unbound Novixa film does not inherit the Famixa character set');
ok(characterDeskForFilm({ filmTypeId: '01a17637-7bfd-7fa8-ad20-4f75f392114f', seriesCode: 'NOVIXA' }) === 'unconfigured', 'a typed film without a Famixa workflow does not inherit the set');
const studioUi = readFileSync(join(root, 'ContentFamixaCharacterStudio.tsx'), 'utf8');
const controller = readFileSync(join(root, '../../../../../src/KitPlatform.Api/Controllers/Content/ContentController.cs'), 'utf8');
ok(tab.includes('famixaCharacters ? (') && tab.includes('data-character-desk={characterDesk}'), 'the people tab mounts Character Studio only for the owning film');
ok(tab.includes('fetchCharacterStudioList(characterScope)') && tab.includes('if (!famixaCharacters)'), 'a non-owning film does not request the Famixa character list');
ok(studioUi.includes("characterDeskForFilm(scope) === 'famixa'") && studioUi.includes('fetchCharacterStudioList(scope)'), 'Character Studio does not load until the film owns the set');
ok(
  controller.includes('if (!OwnsFamixaCharacterSet())')
    && controller.includes('Array.Empty<CharacterStudioCharacterDto>()')
    && controller.includes('Array.Empty<FamixaCharacterDto>()'),
  'the character APIs return no Famixa rows outside the owning film',
);

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('video pipeline ok');
