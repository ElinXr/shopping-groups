import QRCode from "qrcode-terminal/vendor/QRCode/index.js";
export function QR({ value }: { value: string }) {
  const qr = new QRCode(-1, 1);
  qr.addData(value);
  qr.make();
  const count = qr.getModuleCount();
  const cells: string[] = [];
  for (let r = 0; r < count; r++)
    for (let c = 0; c < count; c++) if (qr.isDark(r, c)) cells.push(`M${c + 4} ${r + 4}h1v1h-1z`);
  return (
    <svg
      className="qr"
      role="img"
      aria-label="QR код за присъединяване към групата"
      viewBox={`0 0 ${count + 8} ${count + 8}`}
      shapeRendering="crispEdges"
    >
      <rect width="100%" height="100%" fill="white" />
      <path d={cells.join("")} fill="black" />
    </svg>
  );
}
