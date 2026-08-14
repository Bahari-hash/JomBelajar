(function () {
  var storageKey = "tinylang.admin.theme";
  var mode = "system";
  try {
    var storedMode = window.localStorage.getItem(storageKey);
    if (
      storedMode === "light" ||
      storedMode === "dark" ||
      storedMode === "system"
    ) {
      mode = storedMode;
    }
  } catch {
    mode = "system";
  }
  var prefersDark =
    window.matchMedia &&
    window.matchMedia("(prefers-color-scheme: dark)").matches;
  var isDark = mode === "dark" || (mode === "system" && prefersDark);
  document.documentElement.classList.toggle("dark", isDark);
  document.documentElement.style.colorScheme = isDark ? "dark" : "light";
})();
