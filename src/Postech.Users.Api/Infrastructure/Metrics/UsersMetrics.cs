using Prometheus;

namespace postech.Users.Api.Infrastructure.Metrics;

public static class UsersMetrics
{
    public static readonly Counter UsersRegistered = Prometheus.Metrics.CreateCounter(
        "users_registered_total", "Total number of user registrations");

    public static readonly Counter UsersLoggedIn = Prometheus.Metrics.CreateCounter(
        "users_logged_in_total", "User login attempts",
        new CounterConfiguration { LabelNames = ["status"] });
}
