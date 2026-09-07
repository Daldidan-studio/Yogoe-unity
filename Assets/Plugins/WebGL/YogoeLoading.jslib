mergeInto(LibraryManager.library, {
  YogoeHideLoadingOverlay: function () {
    if (typeof window.YogoeHideLoadingOverlayJs === "function") {
      window.YogoeHideLoadingOverlayJs();
      return;
    }
    var el = document.getElementById("unity-loading-bar");
    if (el) el.style.display = "none";
  }
});
