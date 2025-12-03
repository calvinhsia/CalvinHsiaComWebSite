# Application Insights Telemetry Queries

This document provides KQL (Kusto Query Language) queries for analyzing telemetry data from the WordScape Blazor WASM application.

## Table of Contents
- [Exception Queries](#exception-queries)
- [Event Queries](#event-queries)
- [Performance Queries](#performance-queries)
- [User Behavior Queries](#user-behavior-queries)
- [Error Analysis](#error-analysis)
- [JavaScript Error Queries](#javascript-error-queries)

---

## Exception Queries

### All Exceptions in Last 24 Hours
```kql
exceptions
| where timestamp > ago(24h)
| project timestamp, type, outerMessage, innerMessage, operation_Name, client_Browser, client_OS
| order by timestamp desc
```

### Top 10 Most Frequent Exceptions
```kql
exceptions
| where timestamp > ago(7d)
| summarize count() by type, outerMessage
| top 10 by count_ desc
```

### Exceptions by Browser Type
```kql
exceptions
| where timestamp > ago(7d)
| summarize count() by client_Browser, type
| order by count_ desc
```

### Exception Trends Over Time
```kql
exceptions
| where timestamp > ago(7d)
| summarize count() by bin(timestamp, 1h), type
| render timechart
```

### Exceptions with Custom Properties
```kql
exceptions
| where timestamp > ago(24h)
| extend customProps = todynamic(customDimensions)
| project timestamp, type, outerMessage, customProps
| order by timestamp desc
```

---

## Event Queries

### All Custom Events in Last 24 Hours
```kql
customEvents
| where timestamp > ago(24h)
| project timestamp, name, customDimensions, client_Browser
| order by timestamp desc
```

### Event Counts by Name
```kql
customEvents
| where timestamp > ago(7d)
| summarize count() by name
| order by count_ desc
```

### Game-Specific Events
```kql
customEvents
| where timestamp > ago(7d)
| where name has_any ("WordScape", "Logo", "Fish", "Bounce", "Cartoon", "Wordament")
| summarize count() by name, bin(timestamp, 1h)
| render timechart
```

### User Engagement by Event
```kql
customEvents
| where timestamp > ago(7d)
| summarize users = dcount(user_Id) by name
| order by users desc
```

### Events with Specific Properties
```kql
customEvents
| where timestamp > ago(24h)
| extend props = todynamic(customDimensions)
| where isnotempty(props.score) or isnotempty(props.level)
| project timestamp, name, props
```

---

## Performance Queries

### All Performance Metrics
```kql
customMetrics
| where timestamp > ago(24h)
| project timestamp, name, value, valueCount
| order by timestamp desc
```

### Average Performance by Metric Name
```kql
customMetrics
| where timestamp > ago(7d)
| summarize avg(value), percentile(value, 95), percentile(value, 99) by name
```

### Performance Trends
```kql
customMetrics
| where timestamp > ago(7d)
| summarize avg(value) by bin(timestamp, 1h), name
| render timechart
```

### Slow Operations (P95 > threshold)
```kql
customMetrics
| where timestamp > ago(24h)
| where name contains "duration" or name contains "time"
| summarize p95 = percentile(value, 95) by name
| where p95 > 1000  // milliseconds
| order by p95 desc
```

---

## User Behavior Queries

### Daily Active Users
```kql
union pageViews, customEvents
| where timestamp > ago(30d)
| summarize dau = dcount(user_Id) by bin(timestamp, 1d)
| render timechart
```

### Session Duration Analysis
```kql
pageViews
| where timestamp > ago(7d)
| summarize session_duration = max(timestamp) - min(timestamp) by session_Id
| summarize avg(session_duration), percentile(session_duration, 50), percentile(session_duration, 95)
```

### Most Popular Pages
```kql
pageViews
| where timestamp > ago(7d)
| summarize count() by name, operation_Name
| order by count_ desc
```

### User Journey Analysis
```kql
union pageViews, customEvents
| where timestamp > ago(24h)
| where isnotempty(session_Id)
| project timestamp, session_Id, type = iif(itemType == "pageView", name, strcat("Event:", name))
| order by session_Id, timestamp
```

### Browser and OS Distribution
```kql
pageViews
| where timestamp > ago(7d)
| summarize users = dcount(user_Id) by client_Browser, client_OS
| order by users desc
```

---

## Error Analysis

### Error Rate Over Time
```kql
union exceptions, 
    (customEvents | where name == "JavaScriptError")
| where timestamp > ago(7d)
| summarize errors = count() by bin(timestamp, 1h)
| render timechart
```

### Errors by Page/Operation
```kql
exceptions
| where timestamp > ago(7d)
| summarize count() by operation_Name
| order by count_ desc
```

### Impact Analysis (Errors vs Total Requests)
```kql
let errors = exceptions
    | where timestamp > ago(24h)
    | summarize errorCount = count();
let total = pageViews
    | where timestamp > ago(24h)
    | summarize totalViews = count();
errors
| extend totalViews = toscalar(total)
| extend errorRate = (errorCount * 100.0) / totalViews
| project errorCount, totalViews, errorRate
```

### Unique Error Types
```kql
exceptions
| where timestamp > ago(7d)
| summarize firstSeen = min(timestamp), lastSeen = max(timestamp), occurrences = count() 
    by type, outerMessage
| order by occurrences desc
```

---

## JavaScript Error Queries

### All JavaScript Errors
```kql
customEvents
| where name == "JavaScriptError"
| where timestamp > ago(24h)
| extend errorDetails = todynamic(customDimensions)
| project timestamp, 
    message = tostring(errorDetails.message),
    source = tostring(errorDetails.source),
    lineNumber = tostring(errorDetails.lineNumber),
    columnNumber = tostring(errorDetails.columnNumber),
    userAgent = tostring(errorDetails.userAgent)
| order by timestamp desc
```

### JavaScript Errors by Source File
```kql
customEvents
| where name == "JavaScriptError"
| where timestamp > ago(7d)
| extend errorDetails = todynamic(customDimensions)
| summarize count() by source = tostring(errorDetails.source)
| order by count_ desc
```

### JavaScript Error Trends
```kql
customEvents
| where name == "JavaScriptError"
| where timestamp > ago(7d)
| summarize count() by bin(timestamp, 1h)
| render timechart
```

---

## Advanced Queries

### Funnel Analysis (User Flow Through Game)
```kql
customEvents
| where timestamp > ago(7d)
| where name in ("GameStarted", "LevelCompleted", "GameCompleted")
| summarize count() by name, bin(timestamp, 1d)
| render columnchart
```

### Retention Analysis (Users Returning)
```kql
let firstVisit = pageViews
    | summarize firstVisit = min(timestamp) by user_Id;
pageViews
| join kind=inner (firstVisit) on user_Id
| where timestamp > firstVisit + 1d
| summarize returningUsers = dcount(user_Id) by bin(timestamp, 1d)
| render timechart
```

### Anomaly Detection
```kql
customEvents
| where timestamp > ago(7d)
| summarize count() by bin(timestamp, 1h)
| render anomalychart
```

### Cohort Analysis (Users by First Visit Date)
```kql
let cohorts = pageViews
    | summarize firstSeen = min(timestamp) by user_Id
    | extend cohort = startofweek(firstSeen);
pageViews
| join kind=inner (cohorts) on user_Id
| summarize users = dcount(user_Id) by cohort, week = startofweek(timestamp)
| order by cohort, week
```

### Cross-Platform Comparison
```kql
pageViews
| where timestamp > ago(7d)
| extend platform = case(
    client_OS contains "iOS" or client_OS contains "iPhone", "iOS",
    client_OS contains "Android", "Android",
    client_OS contains "Windows", "Windows",
    client_OS contains "Mac", "macOS",
    "Other"
)
| summarize users = dcount(user_Id), sessions = dcount(session_Id) by platform
| order by users desc
```

---

## Alerting Queries

### High Error Rate Alert
```kql
let threshold = 10; // errors per hour
exceptions
| where timestamp > ago(1h)
| summarize count()
| where count_ > threshold
```

### Performance Degradation Alert
```kql
let baseline = customMetrics
    | where timestamp between (ago(7d) .. ago(24h))
    | where name == "PageLoadTime"
    | summarize baseline = avg(value);
customMetrics
| where timestamp > ago(1h)
| where name == "PageLoadTime"
| summarize current = avg(value)
| extend baseline = toscalar(baseline)
| where current > baseline * 1.5  // 50% slower than baseline
```

### No Activity Alert (Service Down?)
```kql
pageViews
| where timestamp > ago(15m)
| summarize count()
| where count_ == 0
```

---

## Usage Notes

1. **Time Ranges**: Adjust `ago(Xd)` or `ago(Xh)` to change the time window
2. **Custom Dimensions**: Use `todynamic(customDimensions)` to parse custom properties
3. **Rendering**: Add `| render timechart`, `| render columnchart`, etc. for visualizations
4. **Filtering**: Combine with `where` clauses for specific games, users, or conditions
5. **Performance**: Use `summarize` and `bin()` for large datasets

## Running Queries

1. Open Azure Portal
2. Navigate to Application Insights resource
3. Go to **Logs** under Monitoring section
4. Paste query and click **Run**
5. Save useful queries for quick access

## Related Documentation

- [TelemetryService.cs](../Client/Services/TelemetryService.cs) - Client-side telemetry implementation
- [KQL Quick Reference](https://learn.microsoft.com/en-us/azure/data-explorer/kql-quick-reference)
- [Application Insights Overview](https://learn.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)
