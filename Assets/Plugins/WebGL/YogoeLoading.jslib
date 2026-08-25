mergeInto(LibraryManager.library, {
  YogoeHideLoadingOverlay: function () {
    var el = document.getElementById("unity-loading-bar");
    if (el) el.style.display = "none";
  }
});
