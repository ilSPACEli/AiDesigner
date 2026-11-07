using AiDesigner.Models;

namespace AiDesigner.Repositories;

/// <summary>
/// Сховище збережених робіт галереї. Дві реалізації: у пам'яті (для тестів) і EF (PostgreSQL).
/// Обидві мають поводитися однаково: це перевіряють спільні контрактні тести.
/// Очікувані ситуації («немає такої роботи») — це результат (<c>null</c>, <c>false</c>), а не виняток.
/// </summary>
public interface IResultRepository
{

    Task<SavedResult> AddAsync(SavedResult result, int evictOldest = 0, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedResult>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SavedResult?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedResult>> GetOldestAsync(int count, CancellationToken cancellationToken = default);
}