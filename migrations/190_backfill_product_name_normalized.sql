-- Ensure product_name_normalized is populated so GIN trgm similar-clusters can use the index.

UPDATE products
SET product_name_normalized = lower(trim(regexp_replace(product_name, '\s+', ' ', 'g')))
WHERE deleted_at IS NULL
  AND (
    product_name_normalized IS NULL
    OR btrim(product_name_normalized) = ''
  );
