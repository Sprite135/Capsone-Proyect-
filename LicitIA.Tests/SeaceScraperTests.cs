using Xunit;
using LicitIA.Api.Services;

namespace LicitIA.Tests;

public class SeaceScraperTests
{
    [Fact]
    public void ParseSeaceDate_ValidFormat_ReturnsDateTime()
    {
        // Arrange
        var scraper = new SeaceScraperService(new HttpClient());
        var dateStr = "23/04/2026 11:04";
        
        // Act
        var result = scraper.TestParseSeaceDate(dateStr);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(2026, result.Value.Year);
        Assert.Equal(4, result.Value.Month);
        Assert.Equal(23, result.Value.Day);
    }
    
    [Fact]
    public void ParseSeaceDate_InvalidFormat_ReturnsNull()
    {
        // Arrange
        var scraper = new SeaceScraperService(new HttpClient());
        var dateStr = "invalid-date";
        
        // Act
        var result = scraper.TestParseSeaceDate(dateStr);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void ParseSeaceAmount_ValidNumber_ReturnsDecimal()
    {
        // Arrange
        var scraper = new SeaceScraperService(new HttpClient());
        var amountStr = "100000.50";
        
        // Act
        var result = scraper.TestParseSeaceAmount(amountStr);
        
        // Assert
        Assert.Equal(100000.50m, result);
    }
    
    [Fact]
    public void ParseSeaceAmount_WithSpaces_ReturnsDecimal()
    {
        // Arrange
        var scraper = new SeaceScraperService(new HttpClient());
        var amountStr = "100 000.50";
        
        // Act
        var result = scraper.TestParseSeaceAmount(amountStr);
        
        // Assert
        Assert.Equal(100000.50m, result);
    }
}
