// Restore only a validated appearance hint before the app paints; account settings win after login.
(() => {
  let theme = "light";
  try {
    const saved = localStorage.getItem("shopping-appearance-last");
    if (saved === "dark" || saved === "light") theme = saved;
  } catch { /* Storage is optional. */ }
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#111c18" : "#176753");
})();
