-- Local OS: expire events whose calendar day already passed (VN). Isolated park.
-- Does not invent dates. News without a date stay. Do not replay 302.

UPDATE pack_local.listing
SET status = 'EXPIRED', updated_at = NOW()
WHERE kind IN ('event', 'grant')
  AND status = 'ACTIVE'
  AND COALESCE(end_at, start_at) IS NOT NULL
  AND (timezone('Asia/Ho_Chi_Minh', COALESCE(end_at, start_at)))::date
      < (timezone('Asia/Ho_Chi_Minh', NOW()))::date;

UPDATE pack_local.listing
SET status = 'EXPIRED', updated_at = NOW()
WHERE kind IN ('event', 'grant')
  AND status = 'ACTIVE'
  AND COALESCE(end_at, start_at) IS NULL
  AND (
      title ~* 'đã kết thúc|đã diễn ra|đã bế mạc'
      OR summary ~* 'đã kết thúc|đã diễn ra|đã bế mạc'
      OR title ILIKE '%Chợ tình Xuân Dương%'
      OR title ILIKE '%iFEST 2025%'
      OR title LIKE '%27/7/2026%'
      OR (title ILIKE '%Hội chợ%' AND title ILIKE '%OCOP%' AND summary LIKE '%29/4%')
  );
