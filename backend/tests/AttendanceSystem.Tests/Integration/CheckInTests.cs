using System.Net;
using System.Net.Http.Json;
using AttendanceSystem.Api.Services;
using FluentAssertions;

namespace AttendanceSystem.Tests.Integration;

/// <summary>Black-box tests (BB-xx) against the real HTTP pipeline.</summary>
public class CheckInTests : IAsyncLifetime
{
    private readonly Scenario _s = new();
    private HttpClient _prof = null!;
    private SessionDto _session = null!;
    private string _token = null!;

    public async Task InitializeAsync()
    {
        await _s.InitAsync();
        _prof = await _s.LoginAsync("prof@test.dev");
        _session = await _s.StartSessionAsync(_prof);
        _token = await _s.CurrentTokenAsync(_prof, _session.Id);
    }

    public Task DisposeAsync() { _s.Dispose(); return Task.CompletedTask; }

    [Fact(DisplayName = "BB-01 valid QR + GPS -> 201 Created")]
    public async Task BB01_ValidCheckIn_Returns201()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        var res = await _s.CheckInAsync(student, _session.Id, _token, Scenario.OffsetNorth(10), Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await res.Content.ReadFromJsonAsync<AttendanceRecordDto>())!;
        body.Status.Should().Be("Present");
    }

    [Fact(DisplayName = "BB-02 expired QR -> 410 Gone")]
    public async Task BB02_ExpiredQr_Returns410()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        _s.Factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var res = await _s.CheckInAsync(student, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact(DisplayName = "BB-03 GPS 45 m away -> 403 Forbidden")]
    public async Task BB03_TooFar_Returns403()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        var res = await _s.CheckInAsync(student, _session.Id, _token, Scenario.OffsetNorth(45), Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).Should().Contain("distanceMeters");
    }

    [Fact(DisplayName = "BB-04 student not enrolled -> 403 Forbidden")]
    public async Task BB04_NotEnrolled_Returns403()
    {
        var outsider = await _s.LoginAsync("s3@test.dev");
        var res = await _s.CheckInAsync(outsider, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BB-05 duplicate check-in -> 409 Conflict")]
    public async Task BB05_Duplicate_Returns409()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        (await _s.CheckInAsync(student, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon)).StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await _s.CheckInAsync(student, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "BB-06 no auth header -> 401 Unauthorized")]
    public async Task BB06_NoAuth_Returns401()
    {
        var anonymous = _s.Factory.CreateClient();
        var res = await _s.CheckInAsync(anonymous, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BB-07 ended session -> 409 Conflict")]
    public async Task BB07_EndedSession_Returns409()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        (await _prof.PostAsync($"/api/sessions/{_session.Id}/end", null)).EnsureSuccessStatusCode();
        var res = await _s.CheckInAsync(student, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "BB-08 check-in after 10 minutes is marked Late")]
    public async Task BB08_LateCheckIn_IsMarkedLate()
    {
        var student = await _s.LoginAsync("s2@test.dev");
        _s.Factory.Clock.Advance(TimeSpan.FromMinutes(11));
        var fresh = await _s.CurrentTokenAsync(_prof, _session.Id);   // old token expired -> new one issued on demand
        var res = await _s.CheckInAsync(student, _session.Id, fresh, Scenario.RoomLat, Scenario.RoomLon);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await res.Content.ReadFromJsonAsync<AttendanceRecordDto>())!.Status.Should().Be("Late");
    }

    [Fact(DisplayName = "BB-09 missing GPS coordinates -> 400 Bad Request")]
    public async Task BB09_NullGps_Returns400()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        var res = await _s.CheckInAsync(student, _session.Id, _token, null, null);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "Professor live view lists the student after check-in")]
    public async Task Live_ShowsCheckedInStudent()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        await _s.CheckInAsync(student, _session.Id, _token, Scenario.RoomLat, Scenario.RoomLon);
        var live = (await _prof.GetFromJsonAsync<LiveSessionDto>($"/api/sessions/{_session.Id}/live"))!;
        live.CheckedIn.Should().Be(1);
        live.Enrolled.Should().Be(2);
        live.Entries.Single().FullName.Should().Be("Student One");
    }

    [Fact(DisplayName = "A student cannot start a session (role check)")]
    public async Task Student_CannotStartSession()
    {
        var student = await _s.LoginAsync("s1@test.dev");
        var res = await student.PostAsJsonAsync("/api/sessions", new { courseId = _s.CourseId, classroomId = _s.RoomId });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
