using AiDesigner.Services.Results;

namespace AiDesigner.Tests.Services;

// Правило R7 і AC-23: ліміт галереї, витіснення найстаріших.
public class GalleryRetentionPolicyTests
{
    // ---------- Менше ліміту ----------

    [Theory]
    [InlineData(5, 0)]
    [InlineData(5, 2)]
    [InlineData(5, 3)]
    [InlineData(100, 42)]
    public void R7_BelowLimit_EvictsNothing(int limit, int currentCount)
    {
        var policy = new GalleryRetentionPolicy(limit);

        var evict = policy.CountToEvict(currentCount);

        Assert.Equal(0, evict);
    }

    // ---------- Рівно: нова робота займає останнє вільне місце ----------

    [Theory]
    [InlineData(5, 4)]
    [InlineData(3, 2)]
    [InlineData(100, 99)]
    public void R7_NewOneTakesLastFreeSlot_EvictsNothing(int limit, int currentCount)
    {
        var policy = new GalleryRetentionPolicy(limit);

        var evict = policy.CountToEvict(currentCount);

        Assert.Equal(0, evict);
    }

    // ---------- Галерея повна: витісняється одна ----------

    [Theory]
    [InlineData(3, 3)]   // AC-23: N = 3, зберігаємо четверту
    [InlineData(5, 5)]
    [InlineData(100, 100)]
    public void R7_GalleryFull_EvictsExactlyOne(int limit, int currentCount)
    {
        var policy = new GalleryRetentionPolicy(limit);

        var evict = policy.CountToEvict(currentCount);

        Assert.Equal(1, evict);
    }

    // ---------- Понад ліміт: напр., ліміт зменшили в налаштуваннях ----------

    [Theory]
    [InlineData(3, 10, 8)]
    [InlineData(5, 7, 3)]
    [InlineData(100, 150, 51)]
    public void R7_AboveLimit_EvictsEnoughForNewOneToFit(int limit, int currentCount, int expected)
    {
        var policy = new GalleryRetentionPolicy(limit);

        var evict = policy.CountToEvict(currentCount);

        Assert.Equal(expected, evict);
    }

    // ---------- N = 1: у галереї завжди остання робота ----------

    [Theory]
    [InlineData(0, 0)]   // порожня галерея — витісняти нічого
    [InlineData(1, 1)]   // нова замінює єдину стару
    [InlineData(4, 4)]   // навіть якщо накопичилося більше — лишається лише нова
    public void R7_LimitOne_NewOneReplacesAllOld(int currentCount, int expected)
    {
        var policy = new GalleryRetentionPolicy(maxItems: 1);

        var evict = policy.CountToEvict(currentCount);

        Assert.Equal(expected, evict);
    }

    // ---------- Властивості, які мають діяти завжди ----------

    [Fact]
    public void R7_ForAnyLimitAndCount_NewOneFitsAndIsNeverEvicted()
    {
        for (var limit = 1; limit <= 10; limit++)
        {
            var policy = new GalleryRetentionPolicy(limit);

            for (var count = 0; count <= 30; count++)
            {
                var evict = policy.CountToEvict(count);
                var afterSave = count - evict + 1;   // старі за мінусом витіснених + нова

                Assert.True(afterSave <= limit, $"limit={limit}, count={count}: після збереження {afterSave} > ліміту");
                Assert.True(evict <= count, $"limit={limit}, count={count}: витісняємо більше, ніж було, тобто зачепили б нову");
                Assert.True(evict == 0 || afterSave == limit, $"limit={limit}, count={count}: витіснили зайве");
            }
        }
    }

    // ---------- Некоректні вхідні дані ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void R7_NonPositiveLimit_IsRejected(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GalleryRetentionPolicy(limit));
    }

    [Fact]
    public void R7_NegativeCount_IsRejected()
    {
        var policy = new GalleryRetentionPolicy(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.CountToEvict(-1));
    }

    [Fact]
    public void R7_MaxItems_ReturnsConfiguredLimit()
    {
        var policy = new GalleryRetentionPolicy(7);

        Assert.Equal(7, policy.MaxItems);
    }
}