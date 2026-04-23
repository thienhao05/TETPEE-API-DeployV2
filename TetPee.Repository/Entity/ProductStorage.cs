using TetPee.Repository.Abtraction;

namespace TetPee.Repository.Entity;

public class ProductStorage: BaseEntity<Guid>, IAuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; }
    
    public Guid StorageId { get; set; }
    public Storage Storage { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}