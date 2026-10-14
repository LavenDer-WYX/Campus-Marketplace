

document.addEventListener("DOMContentLoaded", function () {
  var searchInput = document.getElementById("searchInput");
  var searchButton = document.getElementById("searchButton");
  var categoryButtons = document.querySelectorAll(".category-btn");
  var itemCards = document.querySelectorAll(".item-card");
  var noResultText = document.getElementById("noResultText");


  if (!searchInput || itemCards.length === 0) {
    return;
  }

  var activeCategory = "all";


  function applyFilters() {
    var keyword = searchInput.value.trim().toLowerCase();
    var visibleCount = 0;

    itemCards.forEach(function (card) {
      var matchCategory =
        activeCategory === "all" || card.dataset.category === activeCategory;
      var matchKeyword =
        keyword === "" || card.dataset.title.indexOf(keyword) !== -1;

      if (matchCategory && matchKeyword) {
        card.style.display = "";
        visibleCount++;
      } else {
        card.style.display = "none";
      }
    });


    if (noResultText) {
      noResultText.hidden = visibleCount !== 0;
    }
  }


  searchButton.addEventListener("click", applyFilters);
  searchInput.addEventListener("input", applyFilters);
  searchInput.addEventListener("keydown", function (event) {
    if (event.key === "Enter") {
      event.preventDefault();
      applyFilters();
    }
  });


  categoryButtons.forEach(function (button) {
    button.addEventListener("click", function () {
      activeCategory = button.dataset.category;

      categoryButtons.forEach(function (b) {
        b.classList.remove("btn-dark");
        b.classList.add("btn-outline-secondary");
      });
      button.classList.remove("btn-outline-secondary");
      button.classList.add("btn-dark");

      applyFilters();
    });
  });

});
