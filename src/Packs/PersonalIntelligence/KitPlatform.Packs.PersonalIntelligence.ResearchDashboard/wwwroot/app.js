const main = document.querySelector("#main");
const views = [...document.querySelectorAll("nav button")];
let documentData = null;
let view = "overview";

views.forEach((button) => {
  button.addEventListener("click", () => {
    view = button.dataset.view;
    views.forEach((item) => item.classList.toggle("active", item === button));
    render();
  });
});
document.querySelector("#refresh").addEventListener("click", load);

function stamp(value) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString() + " · " + value;
}

function money(value) {
  return value === null || value === undefined || value === "" ? "—" : value;
}

function pnl(value) {
  if (!value || !value.known) return `<span class="unknown">Chưa xác định (${value?.status || "unavailable"})</span>`;
  return value.amount;
}

function table(headers, rows) {
  if (rows.length === 0) return `<p class="state">Chưa có dữ liệu.</p>`;
  return `<table><thead><tr>${headers.map((item) => `<th>${item}</th>`).join("")}</tr></thead><tbody>${rows.join("")}</tbody></table>`;
}

function render() {
  if (!documentData) return;
  const data = documentData;
  if (view === "overview") {
    const item = data.overview;
    main.innerHTML = `<section><h2>Trạng thái</h2><div class="metrics">
      <div class="metric"><span>Lần chạy gần nhất</span><strong>${item.status}</strong></div>
      <div class="metric"><span>Thời điểm</span><strong>${stamp(item.lastRunAt)}</strong></div>
      <div class="metric"><span>Nến đã thu</span><strong>${item.candles}</strong></div>
      <div class="metric"><span>Tín hiệu</span><strong>${item.signals}</strong></div>
      <div class="metric"><span>Paper fills</span><strong>${item.paperFills}</strong></div>
      <div class="metric"><span>Replay</span><strong>${item.replays}</strong></div>
      <div class="metric"><span>Tổng phí</span><strong>${money(item.fees)}</strong></div>
      <div class="metric"><span>Tổng slippage</span><strong>${money(item.slippage)}</strong></div>
      <div class="metric"><span>Cash từ fill</span><strong>${money(item.cashFromFills)}</strong></div>
      <div class="metric"><span>Realized PnL</span><strong>${pnl(item.realizedPnl)}</strong></div>
    </div><p class="note">Cash là tổng net_cash của paper fill. Realized PnL chỉ hiện khi phiên gần nhất có trạng thái realized. Không tính mark-to-market.</p></section>`;
  } else if (view === "market") {
    const rows = data.candles.map((row) => `<tr><td>${row.symbol}</td><td>${row.interval}</td><td>${stamp(row.openTime)}</td><td>${stamp(row.closeTime)}</td><td class="${row.closed ? "closed" : "open"}">${row.closed ? "Đã đóng" : "Chưa hoàn tất"}</td><td>${row.open} / ${row.high} / ${row.low} / ${row.close}</td></tr>`);
    main.innerHTML = `<section><h2>Market Data</h2>${table(["Symbol", "Timeframe", "Mở", "Đóng", "Trạng thái", "OHLC"], rows)}<p class="note">Đang hiện ${data.candles.length} dòng trên tổng ${data.overview.candles} nến đã thu. Research host chỉ lưu nến đã đóng. Dòng chưa hoàn tất chỉ xuất hiện khi close_time còn sau thời điểm đọc.</p></section>`;
  } else if (view === "signals") {
    const rows = data.signals.map((row) => `<tr><td>${row.strategy}</td><td>${stamp(row.signalTime)}</td><td>${row.side}</td><td>${row.status}</td><td>${row.riskAllowed === null ? "—" : row.riskAllowed ? "allowed" : "refused"}</td><td>${row.riskReason || "—"}</td></tr>`);
    main.innerHTML = `<section><h2>Signals & Risk</h2>${table(["Chiến lược", "Thời điểm", "Loại", "Trạng thái", "Risk", "Lý do"], rows)}</section>`;
  } else if (view === "trades") {
    const rows = data.fills.map((row) => `<tr><td>${row.side}</td><td>${row.quantity}</td><td>${row.tapePrice}</td><td>${row.executionPrice}</td><td>${row.fee}</td><td>${row.slippageQuote}</td><td>${row.netCash}</td></tr>`);
    main.innerHTML = `<section><h2>Paper Trades</h2>${table(["Side", "Khối lượng", "Giá tape", "Giá khớp", "Phí", "Slippage", "Cash flow"], rows)}<p class="note">PnL của từng lệnh không được lưu. ${pnl(data.overview.realizedPnl)} là PnL của phiên gần nhất, không phải mark-to-market.</p></section>`;
  } else {
    const rows = data.runs.map((row) => `<tr><td>${stamp(row.recordedAt)}</td><td>${row.status}</td><td>${row.inserted ?? "—"}</td><td>${row.duplicates ?? "—"}</td><td>${row.newFills ?? "—"}</td><td>${row.replayed === null ? "—" : row.replayed}</td><td>${row.detail}</td></tr>`);
    main.innerHTML = `<section><h2>Run History</h2>${table(["Thời điểm", "Trạng thái", "Mới", "Trùng", "Fill mới", "Replay", "Chi tiết"], rows)}</section>`;
  }
}

async function load() {
  main.innerHTML = `<p class="state">Đang tải dữ liệu research.</p>`;
  try {
    const headers = {};
    const token = sessionStorage.getItem("research-dashboard-token");
    if (token) headers.Authorization = "Bearer " + token;
    const response = await fetch("/api/dashboard", { headers });
    if (response.status === 401) {
      const entered = window.prompt("Token đọc dashboard");
      if (entered) sessionStorage.setItem("research-dashboard-token", entered);
      main.innerHTML = `<p class="state error">Cần xác thực. Bấm Tải lại sau khi nhập token.</p>`;
      return;
    }
    if (!response.ok) throw new Error(String(response.status));
    documentData = await response.json();
    document.querySelector("#source").textContent = documentData.source === "fixture" ? "Nguồn fixture cục bộ" : "Nguồn research_trading";
    document.querySelector("#generated").textContent = stamp(documentData.generatedAt);
    render();
  } catch (error) {
    document.querySelector("#source").textContent = "Mất kết nối";
    main.innerHTML = `<p class="state error">Không kết nối được nguồn dữ liệu research.</p>`;
  }
}

load();
