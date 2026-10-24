declare module "qrcode-terminal/vendor/QRCode/index.js" {
  export default class QRCode {
    constructor(version: number, correctionLevel: number);
    addData(value: string): void;
    make(): void;
    getModuleCount(): number;
    isDark(row: number, col: number): boolean;
  }
}
