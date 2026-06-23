/**
 * Kiểm tra token fanpage (không in token ra màn hình).
 */
import { loadFanpageCredentials } from './fanpage-config.mjs';

const GRAPH_VERSION = 'v21.0';

async function main() {
  const creds = loadFanpageCredentials();
  if (!creds) {
    console.error('Chưa có FB_PAGE_ID/FB_PAGE_ACCESS_TOKEN hoặc import/Id_Fanpage.txt');
    process.exit(1);
  }

  const res = await fetch(
    `https://graph.facebook.com/${GRAPH_VERSION}/${creds.pageId}?fields=id,name`,
    { headers: { Authorization: `Bearer ${creds.accessToken}` } },
  );
  const data = await res.json();

  if (!res.ok) {
    console.error('Token không hợp lệ hoặc không truy cập được Page ID', creds.pageId);
    console.error(data?.error?.message ?? res.statusText);
    console.error('\nCách sửa: Graph API Explorer → quyền pages_manage_posts + pages_read_engagement');
    console.error('→ GET me/accounts → copy access_token của fanpage (Page Token).');
    process.exit(1);
  }

  console.log('OK — Page:', data.name, `(${data.id})`);
  console.log('Nguồn creds:', creds.source);
  console.log('Chạy thử: npm run post:fanpage:dry');
}

main();
