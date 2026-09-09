import { useEffect, useState } from 'react';
import { Alert, Card, Collapse, Tag } from 'antd';
import { fetchCharacterDesignLanguage, type CharacterDesignLanguageDefinition } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { designLanguageStatusLabel } from './kit-video-character-design-language';

export function ContentFamixaCharacterDesignLanguageCard() {
  const [row, setRow] = useState<CharacterDesignLanguageDefinition>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    void fetchCharacterDesignLanguage()
      .then((next) => {
        setRow(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Character Design Language.')));
  }, []);

  return (
    <Card className="fx-look__card fx-pvs fx-pvs--director" size="small" title="CHARACTER DESIGN LANGUAGE">
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <div className="fx-pvs__summary">
        <div>
          <p className="fx-pvs__kicker">Status</p>
          <Tag color={row?.status === 'ACTIVE' ? 'green' : 'default'}>{designLanguageStatusLabel(row?.status)}</Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Stylization</p>
          <p>
            <b>{row?.stylizationLevel || 'STRONG'}</b>
          </p>
          <p className="fx-desk__note">Photorealism {row?.photorealismLevel || 'LOW'}</p>
        </div>
        <div>
          <p className="fx-pvs__kicker">Age adaptive</p>
          <p>
            <b>{row?.ageAdaptiveStylization === false ? 'NO' : 'YES'}</b>
          </p>
          <p className="fx-desk__note">
            Character-specific branching {row?.characterSpecificBranching ? 'YES' : 'NO'}
          </p>
        </div>
      </div>
      <p className="fx-desk__note">Shared character-construction language — not identity, age policy, or PVS.</p>
      <Collapse
        ghost
        className="fx-director-tech"
        items={[
          {
            key: 'cdl-tech',
            label: 'Chi tiết Character Design Language',
            children: (
              <div>
                <p>Face</p>
                <p>{row?.faceLanguage}</p>
                <p>Eyes</p>
                <p>{row?.eyeLanguage}</p>
                <p>Skin</p>
                <p>{row?.skinLanguage}</p>
                <p>Hair</p>
                <p>{row?.hairLanguage}</p>
                <p>Body</p>
                <p>{row?.bodyLanguage}</p>
                <p>Expression</p>
                <p>{row?.expressionLanguage}</p>
                <p>Negative constraints</p>
                <p>{row?.negativePromptBlock}</p>
                <p>Prompt / Generation Brief</p>
                <p>{row?.positivePromptBlock}</p>
                <p>SHA {row?.sha || '—'}</p>
              </div>
            ),
          },
        ]}
      />
    </Card>
  );
}
