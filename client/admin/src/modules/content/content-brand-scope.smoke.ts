import {
  canWriteInBrand,
  nextFilmParam,
  parseBrandParam,
  readLastBrandId,
  writeLastBrandId,
} from "./content-brand-scope.ts";

let failed = 0;
function ok(cond: boolean, name: string) {
  if (!cond) {
    failed += 1;
    console.error("FAIL " + name);
  } else {
    console.log("ok " + name);
  }
}

const brand = "019ff475-4641-724c-ac3b-21c7eff6d784";
const other = "019fec28-586c-7c7c-907e-38e11152dcf7";
const film = "019f3330-0001-7000-8000-000000000001";

ok(parseBrandParam(null).mode === "missing", "missing brand is not a default");
ok(parseBrandParam("").mode === "missing", "empty brand is not a default");
ok(parseBrandParam("all").mode === "all", "all is explicit");
ok(parseBrandParam("kittech").mode === "invalid", "code is not an id");
const one = parseBrandParam(brand);
ok(one.mode === "one" && one.mode === "one" && one.brandId === brand, "guid is one brand");
ok(canWriteInBrand(one) === true, "one brand can write");
ok(canWriteInBrand({ mode: "all" }) === false, "all is read only");
ok(canWriteInBrand({ mode: "missing" }) === false, "missing cannot write");
ok(nextFilmParam(brand, brand, film) === film, "same brand keeps film");
ok(nextFilmParam(brand, other, film) === null, "other brand drops film");
ok(nextFilmParam(brand, null, film) === null, "clearing brand drops film");

const store = new Map<string, string>();
const storage = {
  getItem: (key: string) => store.get(key) ?? null,
  setItem: (key: string, value: string) => {
    store.set(key, value);
  },
  removeItem: (key: string) => {
    store.delete(key);
  },
};
writeLastBrandId(storage, brand);
ok(readLastBrandId(storage) === brand, "last brand stored");
writeLastBrandId(storage, "all");
ok(readLastBrandId(storage) === null, "all is not stored as a brand");

if (failed > 0) {
  console.error("content-brand-scope FAIL " + failed);
  process.exit(1);
}
console.log("content-brand-scope PASS");
