namespace BookInfoFinder.Models.View;

public sealed record CatalogField(string Name, string Label, string Type = "text", bool Required = false, int MaxLength = 200, string? Hint = null);

public sealed record AdminCatalogDefinition(string Kind, string Title, string Singular, string Description, string ListKey, string IdKey, string Icon, CatalogField[] Fields)
{
    public static AdminCatalogDefinition For(string kind) => kind switch
    {
        "categories" => new(kind, "Thể loại", "thể loại", "Sắp xếp sách thành những chủ đề dễ khám phá.", "categories", "categoryId", "collection", [new("name", "Tên thể loại", Required: true, MaxLength: 50), new("description", "Mô tả", "textarea")]),
        "tags" => new(kind, "Thẻ sách", "thẻ sách", "Kết nối những cuốn sách qua chủ đề và từ khóa.", "tags", "tagId", "tags", [new("name", "Tên thẻ", Required: true, MaxLength: 30), new("description", "Mô tả", "textarea")]),
        "authors" => new(kind, "Tác giả", "tác giả", "Thông tin những người đứng sau mỗi cuốn sách.", "authors", "authorId", "person-badge", [new("name", "Tên tác giả", Required: true, MaxLength: 30), new("nationality", "Quốc tịch", MaxLength: 50), new("dateOfBirth", "Ngày sinh", "date"), new("biography", "Tiểu sử", "textarea", MaxLength: 100)]),
        "publishers" => new(kind, "Nhà xuất bản", "nhà xuất bản", "Quản lý đơn vị xuất bản và thông tin liên hệ.", "publishers", "publisherId", "buildings", [new("name", "Tên nhà xuất bản", Required: true, MaxLength: 50), new("address", "Địa chỉ", MaxLength: 100), new("contactInfo", "Thông tin liên hệ", MaxLength: 100)]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
