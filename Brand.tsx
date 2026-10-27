export function LogoMark({ size = 38, className = "" }: { size?: number; className?: string }) {
  return <img className={`brand-mark ${className}`} src="/icon.svg" width={size} height={size} alt="" />;
}
export function Brand({ compact = false, className = "" }: { compact?: boolean; className?: string }) {
  return (
    <a href="/" className={`brand ${compact ? "brand-compact" : ""} ${className}`} aria-label="Shopping Groups — начало">
      <LogoMark /><span>Shopping Groups</span>
    </a>
  );
}
