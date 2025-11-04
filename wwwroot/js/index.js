const pageSize = 8;
let currentBooks = [];
 
// ======================== LOAD BOOKS ========================
function loadBooks(page = 1) {
   const formData = $("#searchForm").serializeArray();
   const searchParams = new URLSearchParams();
   formData.forEach(({ name, value }) => searchParams.append(name, value));
   searchParams.set("page", page);
   searchParams.set("pageSize", pageSize);
   $.getJSON(`/Index?handler=AjaxSearch&${searchParams.toString()}`, function (data) {
       currentBooks = data.books; // ✅ thêm dòng này
       renderBooks(data.books, page);
       renderPagination("#paginationButtons", data.totalPages, page, loadBooks);
   });
}
 
// ======================== GỢI Ý TÌM KIẾM ========================
function setupSuggest(inputId, suggestId, apiHandler) {
  const $input = $(`#${inputId}`);
  const $suggest = $(`#${suggestId}`);
 
  $input.on("input", function () {
    const keyword = $(this).val();
    if (keyword.length < 2) return $suggest.hide();
 
    $.getJSON(`/Index?handler=${apiHandler}&keyword=${encodeURIComponent(keyword)}`, (data) => {
      $suggest.empty();
      data.forEach((item) => {
        $suggest.append(`<button type="button" class="list-group-item list-group-item-action">${item}</button>`);
      });
      $suggest.toggle(data.length > 0);
      $suggest.find("button").on("click", function () {
        $input.val($(this).text());
        $suggest.hide();
      });
    });
  });
 
  $input.on("blur", () => setTimeout(() => $suggest.hide(), 200));
}
 
// ======================== HIỂN THỊ SÁCH ========================
function renderBooks(books, currentPage = 1) {
  $("#paginationNav").show();
  const $container = $("#bookList").empty();
  const startIndex = (currentPage - 1) * pageSize;
 
  books.forEach((book, index) => {
    const displayIndex = startIndex + index + 1;
    const imageSrc = book.imageBase64 || "/images/default-book.svg";
    const tags = Array.isArray(book.tags) && book.tags.length ? book.tags.join(", ") : "Không có";
    const rating = typeof book.averageRating === "number" ? book.averageRating : 0;
    const ratingCount = typeof book.ratingCount === "number" ? book.ratingCount : 0;
    const authorName = book.authorName ?? "Không rõ";
    const categoryName = book.categoryName ?? "Không rõ";
 
    $container.append(`
      <div class="col-xl-3 col-lg-4 col-md-6 col-sm-12 mb-4 book-card">
        <div class="card h-100 shadow-lg rounded-4">
          <a href="/BookDetail/${book.bookId}" class="text-decoration-none">
            <img src="${imageSrc}" class="card-img-top" alt="${book.title}" style="height: 280px; object-fit: cover;" />
          </a>
          <div class="card-body p-4 d-flex flex-column">
            <a href="/BookDetail/${book.bookId}" class="text-decoration-none d-block mb-3">
              <h5 class="fw-bold text-primary mb-0">${displayIndex}. ${book.title}</h5>
            </a>
            <div class="flex-grow-1">
              <p class="mb-2"><strong class="text-secondary">Tác giả:</strong> <span class="text-dark">${authorName}</span></p>
              <p class="mb-2"><strong class="text-secondary">Thể loại:</strong> <span class="text-dark">${categoryName}</span></p>
              <p class="mb-2"><strong class="text-secondary">Năm xuất bản:</strong> <span class="text-dark">${book.publicationYear}</span></p>
              <p class="mb-2">
                <strong class="text-secondary">Đánh giá:</strong>
                <span class="text-danger fw-bold">${rating} ★</span>
                <span class="text-muted">(${ratingCount} lượt)</span>
              </p>
              <p class="mb-3"><strong class="text-secondary">Tags:</strong> <span class="text-muted">${tags}</span></p>
            </div>
            <a class="btn btn-outline-primary btn-sm fw-semibold rounded-pill mt-auto"
               href="/BookDetail/${book.bookId}">
              📖 Xem chi tiết
            </a>
          </div>
        </div>
      </div>
    `);
  });
}
 
 
// ======================== SUBMIT FORM TÌM KIẾM ========================
$("#searchForm").on("submit", function (e) {
  e.preventDefault();
  const keyword = $("#titleInput").val().trim();
  loadBooks(1);
  setTimeout(() => {
    showToast(`Đã tìm thấy ${currentBooks.length} kết quả cho "${keyword || 'từ khóa'}"`, "success");
  }, 600);
});
 
// ======================== CLICK TAG ========================
function loadBooksByTag(tag, page = 1) {
 
    $.getJSON(`/Index?handler=SearchByTag&tag=${encodeURIComponent(tag)}&page=${page}`, function (data) {
 
        currentBooks = data.books;
 
        renderBooks(data.books, page);
 
        // ❌ Bỏ phân trang cho tag
 
        $("#paginationNav").hide();
 
        showToast(`Sách thuộc tag "${tag}"`, "info");
 
    }).fail(function () {
 
        $("#bookList").html(`<p class="text-danger">Không thể tải sách.</p>`);
 
    });
 
}
 
// Gắn sự kiện trực tiếp (như TopRated / MostFavorited)
$("#tagDropdown").on("change", function () {
   const tagName = $(this).val();
   if (!tagName || tagName.trim() === "") {
       loadBooks(1);
   } else {
       loadBooksByTag(tagName.trim(), 1);
   }
});
// ======================== GỢI Ý NHẬP LIỆU ========================
setupSuggest("titleInput", "titleSuggest", "TitleSuggest");
setupSuggest("authorInput", "authorSuggest", "AuthorSuggest");
setupSuggest("categoryInput", "categorySuggest", "CategorySuggest");
 
// ======================== LOAD LẦN ĐẦU ========================
$(function () {
  loadBooks(1);
});
function loadMostFavorited(page = 1) {
    $.getJSON(`/Index?handler=MostFavorited&page=${page}&pageSize=${pageSize}`, function (data) {
        currentBooks = data.books;
        renderBooks(data.books, page);
        renderPagination("#paginationButtons", data.totalPages, page, loadMostFavorited);
        showToast("Top sách được yêu thích nhất!", "info");
    });
}
 
function loadTopRated(page = 1) {
    $.getJSON(`/Index?handler=TopRated&page=${page}&pageSize=${pageSize}`, function (data) {
        currentBooks = data.books;
        renderBooks(data.books, page);
        renderPagination("#paginationButtons", data.totalPages, page, loadTopRated);
        showToast("Top sách được đánh giá cao nhất!", "success");
    });
}
 
$("#btnMostFavorited").on("click", function () {
    loadMostFavorited(1);
});
 
$("#btnTopRated").on("click", function () {
    loadTopRated(1);
});