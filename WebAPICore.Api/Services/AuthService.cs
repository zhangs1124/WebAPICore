using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebAPICore.Api.Data;
using WebAPICore.Api.Dtos;
using WebAPICore.Api.Models.Entities;

namespace WebAPICore.Api.Services;

/// <summary>
/// 權限與身分驗證服務實作
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext dbContext, ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<User> RegisterAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException($"帳號【{request.Username}】已經存在。");
        }

        var validRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Manager", "Purchaser", "Warehouse" };
        if (!validRoles.Contains(request.Role))
        {
            throw new ArgumentException($"無效的角色【{request.Role}】，有效角色為：Admin, Manager, Purchaser, Warehouse。");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            PasswordHash = HashPassword(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Role = request.Role,
            Department = string.IsNullOrWhiteSpace(request.Department) ? "總務課" : request.Department.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("成功註冊使用者帳號: {Username}, 角色: {Role}, 部門: {Department}", user.Username, user.Role, user.Department);
        return user;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username.Trim(), cancellationToken);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("帳號或密碼錯誤。");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("此帳號已被停用，請聯絡管理員。");
        }

        // 簡易發行 Token（可在後續串接 JWT 或 Session）
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user.Id}:{user.Role}:{DateTime.UtcNow.Ticks}"));

        return new LoginResponse(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.Department,
            token
        );
    }

    /// <inheritdoc />
    public async Task<User?> GetUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username.Trim(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + "AntigravitySalt2026"));
        return Convert.ToHexString(bytes);
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var hash = HashPassword(password);
        return string.Equals(hash, storedHash, StringComparison.OrdinalIgnoreCase);
    }
}

