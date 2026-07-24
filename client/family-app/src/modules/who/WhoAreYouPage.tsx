import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchFamilies, type FamilyMembership } from '@/shared/api/family-os.api';
import { useSessionStore } from '@/shared/auth/session.store';
import { isOnboardingDone } from '@/shared/onboarding/onboarding';
import { hydrateFamilyValueState } from '@/shared/value/value-sync';
import { ParentPinSheet } from '@/shared/ui/ParentPinSheet';
import {
  avatarEmoji,
  avatarToneClass,
  inferGenderFromName,
} from '@/shared/ui/avatarGender';

function roleLabel(role: string): string {
  switch (role) {
    case 'child':
      return 'Con';
    case 'guardian':
      return 'Phụ huynh';
    case 'caregiver':
      return 'Chăm sóc';
    default:
      return role;
  }
}

function MemberButton({
  member,
  big,
  onPick,
}: {
  member: FamilyMembership;
  tone?: number;
  big?: boolean;
  onPick: () => void;
}) {
  const gender = inferGenderFromName(member.displayName);
  return (
    <button
      type="button"
      className={`member-chip${member.roleCode === 'child' ? '' : ' is-guardian'}${big ? ' is-big' : ''}`}
      onClick={onPick}
    >
      <span className={`member-avatar ${avatarToneClass(gender)}`}>
        {avatarEmoji(gender, member.roleCode)}
      </span>
      <span className="member-name">{member.displayName}</span>
      <span className="member-role">{roleLabel(member.roleCode)}</span>
    </button>
  );
}

export function WhoAreYouPage() {
  const navigate = useNavigate();
  const familyId = useSessionStore((s) => s.familyId);
  const familyName = useSessionStore((s) => s.familyName);
  const setMember = useSessionStore((s) => s.setMember);
  const clear = useSessionStore((s) => s.clear);
  const verifyParentPin = useSessionStore((s) => s.verifyParentPin);
  const [members, setMembers] = useState<FamilyMembership[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [pinOpen, setPinOpen] = useState(false);

  useEffect(() => {
    if (!familyId) return;
    void hydrateFamilyValueState(familyId);
  }, [familyId]);

  useEffect(() => {
    let cancelled = false;
    void fetchFamilies()
      .then((families) => {
        if (cancelled) return;
        const family = families.find((f) => f.id === familyId) ?? families[0];
        setMembers(family?.members ?? []);
      })
      .catch(() => {
        if (!cancelled) setError('Không tải được danh sách thành viên');
      });
    return () => {
      cancelled = true;
    };
  }, [familyId]);

  const children = useMemo(
    () => members.filter((m) => m.roleCode === 'child'),
    [members],
  );
  const adults = useMemo(
    () => members.filter((m) => m.roleCode !== 'child'),
    [members],
  );

  const pick = async (picked: FamilyMembership) => {
    setMember(picked);
    if (picked.roleCode !== 'child' && familyId) {
      await hydrateFamilyValueState(familyId);
      if (!isOnboardingDone(familyId)) {
        navigate('/onboarding', { replace: true });
        return;
      }
    }
    navigate('/today', { replace: true });
  };

  return (
    <>
      <header className="stack" style={{ gap: 4 }}>
        <div className="brand-mark">Xin chào!</div>
        <div className="brand-sub">{familyName ?? 'Gia đình mình'}</div>
      </header>

      <section className="card stack">
        <p className="lead">Con là ai hôm nay?</p>
        <p className="muted" style={{ margin: 0 }}>
          Bé: một việc tiếp theo. Bố mẹ: bảng nhà cả nhà.
        </p>
        {error ? <div className="banner-error">{error}</div> : null}

        {children.length > 0 ? (
          <>
            <div className="section-label">Các bạn nhỏ</div>
            <div className={`member-grid${children.length === 1 ? ' is-single' : ''}`}>
              {children.map((member, index) => (
                <MemberButton
                  key={member.id}
                  member={member}
                  tone={index}
                  big={children.length === 1}
                  onPick={() => pick(member)}
                />
              ))}
            </div>
          </>
        ) : null}

        {adults.length > 0 ? (
          <>
            <div className="section-label">Bố mẹ</div>
            <div className="member-grid">
              {adults.map((member, index) => (
                <MemberButton
                  key={member.id}
                  member={member}
                  tone={index + 2}
                  onPick={() => pick(member)}
                />
              ))}
            </div>
          </>
        ) : null}
      </section>

      <button
        type="button"
        className="btn btn-ghost parent-only"
        onClick={() => setPinOpen(true)}
      >
        Đổi thiết bị / đăng xuất
      </button>

      <ParentPinSheet
        open={pinOpen}
        title="Đăng xuất"
        hint="Nhập mã bố mẹ để đăng xuất thiết bị"
        verify={verifyParentPin}
        onClose={() => setPinOpen(false)}
        onSuccess={() => {
          setPinOpen(false);
          clear();
          navigate('/unlock', { replace: true });
        }}
      />
    </>
  );
}
