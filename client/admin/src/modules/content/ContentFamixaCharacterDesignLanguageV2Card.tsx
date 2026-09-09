import { useEffect, useState } from 'react';
import { Alert, Card, Collapse, Tag } from 'antd';
import { fetchCharacterDesignLanguageV2, type CharacterDesignLanguageV2Definition } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { designLanguageStatusLabel } from './kit-video-character-design-language';

export function ContentFamixaCharacterDesignLanguageV2Card() {
  const [row, setRow] = useState<CharacterDesignLanguageV2Definition>();
  const [error, setError] = useState<string>();

  useEffect(() => {
    void fetchCharacterDesignLanguageV2()
      .then((next) => {
        setRow(next);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được Character Design Language V2.')));
  }, []);

  return (
    <Card className="fx-look__card fx-pvs fx-pvs--director" size="small" title="CHARACTER DESIGN LANGUAGE V2">
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      <div className="fx-pvs__summary">
        <div>
          <p className="fx-pvs__kicker">Status</p>
          <Tag color="gold">{designLanguageStatusLabel(row?.status) || 'DRAFT'} / READY FOR REVIEW</Tag>
        </div>
        <div>
          <p className="fx-pvs__kicker">Stylization</p>
          <p>
            <b>{row?.stylizationLevel || 'STRONG'}</b>
          </p>
          <p className="fx-desk__note">{row?.stylizationTarget || 'STRONG_STYLIZED_3D'}</p>
        </div>
        <div>
          <p className="fx-pvs__kicker">Photorealism</p>
          <p>
            <b>{row?.photorealismCeiling || 'LOW'}</b>
          </p>
          <p className="fx-desk__note">Cartoon {row?.cartoonFloor || 'CONTROLLED'}</p>
        </div>
      </div>
      <p className="fx-desk__note">
        Shared FAMIXA character-construction law — not identity, age, role, or current PVS authority.
      </p>
      <Collapse
        ghost
        className="fx-director-tech"
        items={[
          {
            key: 'cdl-v2-tech',
            label: 'Chi tiết kỹ thuật',
            children: (
              <div>
                <p>Face</p>
                <p>{row?.faceLanguage}</p>
                <p>Eyes</p>
                <p>{row?.eyeLanguage}</p>
                <p>Hair</p>
                <p>{row?.hairLanguage}</p>
                <p>Body</p>
                <p>{row?.bodyLanguage}</p>
                <p>Materials</p>
                <p>{row?.skinLanguage}</p>
                <p>Lighting</p>
                <p>{row?.lightingLanguage}</p>
                <p>Realism Ceiling</p>
                <p>{row?.realismCeilingLanguage}</p>
                <p>Cross-character Invariants</p>
                <p>{row?.crossCharacterInvariants}</p>
                <p>SHA {row?.sha256 || '—'}</p>
              </div>
            ),
          },
        ]}
      />
    </Card>
  );
}
