-- Local OS: học bổng / ưu đãi gộp vào sự kiện (không mở /uu-dai). Isolated park.

UPDATE pack_local.listing
SET kind = 'event',
    category = CASE
        WHEN COALESCE(BTRIM(category), '') IN ('', 'grant', 'offer', 'scholarship') THEN 'benefit'
        ELSE category
    END,
    updated_at = NOW()
WHERE kind IN ('grant', 'offer');
