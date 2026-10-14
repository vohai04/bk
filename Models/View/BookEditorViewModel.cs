using BookInfoFinder.Models.Dto;

namespace BookInfoFinder.Models.View;

public sealed class BookEditorViewModel
{
    public BookUpdateDto Book { get; init; } = new();
    public IReadOnlyList<AuthorDto> Authors { get; init; } = [];
    public IReadOnlyList<CategoryDto> Categories { get; init; } = [];
    public IReadOnlyList<PublisherDto> Publishers { get; init; } = [];
    public IReadOnlyList<TagDto> Tags { get; init; } = [];
    public IReadOnlyList<int> SelectedTagIds { get; init; } = [];
    public bool IsEdit => Book.BookId > 0;
}
