-- Local OS: listings from official sources use trust = SOURCE_TRUSTED.
-- 294 only allowed UNVERIFIED / COMMUNITY / VERIFIED. Widen before publish seeds.
-- Isolated park — not Pharmacy.

ALTER TABLE pack_local.listing DROP CONSTRAINT IF EXISTS ck_local_listing_trust;
ALTER TABLE pack_local.listing
    ADD CONSTRAINT ck_local_listing_trust
    CHECK (trust IN ('UNVERIFIED', 'COMMUNITY', 'VERIFIED', 'SOURCE_TRUSTED'));
