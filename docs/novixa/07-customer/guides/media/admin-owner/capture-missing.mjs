import { chromium } from "playwright";
import path from "node:path";
import { fileURLToPath } from "node:url";
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT = __dirname;
const ADMIN = process.env.ADMIN_URL ?? "http://127.0.0.1:5173";
async function wait(page){ await page.locator(".ant-spin-spinning").first().waitFor({state:"hidden",timeout:15000}).catch(()=>{}); await page.waitForTimeout(400); }
async function shot(page,n){ await page.screenshot({path:path.join(OUT,n+".png"), fullPage:false}); console.log("OK",n); }
async function login(page){
  await page.goto(ADMIN+"/login",{waitUntil:"domcontentloaded",timeout:90000});
  await wait(page);
  const d=page.getByRole("button",{name:/DEMO_PHARMACY/i});
  if(await d.isVisible().catch(()=>false)) await d.click();
  else { await page.locator("input").nth(0).fill("DEMO_PHARMACY"); await page.locator("input").nth(1).fill("admin"); await page.locator('input[type="password"]').fill("Admin@123"); }
  await page.locator('form button[type="submit"], button.ant-btn-primary').first().click();
  await page.waitForURL(u=>!u.pathname.includes("/login"),{timeout:60000});
  await wait(page);
}
const browser=await chromium.launch({headless:true});
const page=await (await browser.newContext({viewport:{width:1440,height:900}})).newPage();
try{
  await login(page);
  // 43 customer detail
  await page.goto(ADMIN+"/customer/list",{waitUntil:"domcontentloaded"});
  await wait(page);
  const a=page.locator(".ant-table-tbody a").first();
  if(await a.isVisible().catch(()=>false)){ await a.click(); await wait(page); await page.waitForTimeout(700); await shot(page,"43-khach-detail"); }
  // 37 create adjustment
  await page.goto(ADMIN+"/inventory/adjustments",{waitUntil:"domcontentloaded"});
  await wait(page);
  const create=page.getByRole("button",{name:/Tạo|Thêm/i}).first();
  if(await create.isVisible().catch(()=>false)){ await create.click(); await wait(page); await page.waitForTimeout(500); await shot(page,"37-kiem-ke-tao"); await page.keyboard.press("Escape"); }
  // 40 transfer detail
  await page.goto(ADMIN+"/inventory/transfers",{waitUntil:"domcontentloaded"});
  await wait(page);
  const xem=page.getByRole("button",{name:/Xem|Chi tiết/i}).first();
  if(await xem.isVisible().catch(()=>false)){ await xem.click(); await wait(page); await page.waitForTimeout(500); await shot(page,"40-dieu-chuyen-detail"); }
  // 31 PO edit - open list, xem draft/open PO then sua
  await page.goto(ADMIN+"/procurement/purchase-orders",{waitUntil:"domcontentloaded"});
  await wait(page);
  const xemPo=page.getByRole("button",{name:/Xem|Chi tiết/i}).first();
  if(await xemPo.isVisible().catch(()=>false)){
    await xemPo.click(); await wait(page); await page.waitForTimeout(500);
    const sua=page.getByRole("button",{name:/Sửa|Chỉnh sửa/i}).first();
    if(await sua.isVisible().catch(()=>false)){ await sua.click({force:true}); await wait(page); await page.waitForTimeout(500); await shot(page,"31-po-sua"); }
  }
  console.log("done missing");
} finally { await browser.close(); }