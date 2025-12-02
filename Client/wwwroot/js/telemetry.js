// Telemetry JavaScript Functions for Application Insights

// Version constant for consistent logging
const TELEMETRY_VERSION = 'v1';

// Track a custom event
window.trackEvent = function (eventData) {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] Tracking event:`, eventData.name, 'with', Object.keys(eventData.properties || {}).length, 'properties');
    
    // Use the global appInsights object directly (loaded in index.html)
    if (typeof appInsights !== 'undefined' && appInsights.trackEvent) {
        try {
            appInsights.trackEvent({
                name: eventData.name,
                properties: eventData.properties
            });
            console.log(`? [Telemetry ${TELEMETRY_VERSION}] Event tracked successfully:`, eventData.name);
        } catch (ex) {
            console.error(`? [Telemetry ${TELEMETRY_VERSION}] Failed to track event:`, ex);
        }
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] appInsights not ready yet for event:`, eventData.name);
    }
};

// Track an exception
window.trackException = function (exceptionData) {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] Tracking exception:`, exceptionData.type);
    
    if (typeof appInsights !== 'undefined' && appInsights.trackException) {
        try {
            appInsights.trackException({
                exception: new Error(exceptionData.message),
                properties: exceptionData.properties,
                severityLevel: 3 // Error
            });
            console.log(`? [Telemetry ${TELEMETRY_VERSION}] Exception tracked successfully`);
        } catch (ex) {
            console.error(`? [Telemetry ${TELEMETRY_VERSION}] Failed to track exception:`, ex);
        }
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] appInsights not ready yet`);
    }
}

// Track a JavaScript error specifically
window.trackJavaScriptError = function (errorData) {
    console.error(`[Telemetry ${TELEMETRY_VERSION}] JavaScript error:`, errorData.message);
    
    if (typeof appInsights !== 'undefined' && appInsights.trackException) {
        try {
            appInsights.trackException({
                exception: new Error(errorData.message),
                properties: {
                    ...errorData.properties,
                    type: 'JavaScriptError'
                },
                severityLevel: 3 // Error
            });
            console.log(`? [Telemetry ${TELEMETRY_VERSION}] JS error tracked successfully`);
        } catch (ex) {
            console.error(`? [Telemetry ${TELEMETRY_VERSION}] Failed to track JS error:`, ex);
        }
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] appInsights not ready yet`);
    }
};

// Track a performance metric
window.trackMetric = function (metricData) {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] Tracking metric:`, metricData.name, '=', metricData.value);
    
    if (typeof appInsights !== 'undefined' && appInsights.trackMetric) {
        try {
            appInsights.trackMetric({
                name: metricData.name,
                average: metricData.value,
                properties: metricData.properties
            });
            console.log(`? [Telemetry ${TELEMETRY_VERSION}] Metric tracked successfully`);
        } catch (ex) {
            console.error(`? [Telemetry ${TELEMETRY_VERSION}] Failed to track metric:`, ex);
        }
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] appInsights not ready yet`);
    }
};

// Setup global error handlers to catch all unhandled errors
window.setupGlobalErrorHandlers = function () {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] Setting up global error handlers`);
    
    // Catch unhandled JavaScript errors
    window.addEventListener('error', function (event) {
        console.error(`[Telemetry ${TELEMETRY_VERSION}] Uncaught error:`, event.error || event.message);
        
        if (typeof appInsights !== 'undefined' && appInsights.trackException) {
            try {
                appInsights.trackException({
                    exception: event.error || new Error(event.message),
                    properties: {
                        source: event.filename || 'unknown',
                        lineNumber: event.lineno || 0,
                        columnNumber: event.colno || 0,
                        url: window.location.href
                    },
                    severityLevel: 3 // Error
                });
            } catch (ex) {
                console.error(`[Telemetry ${TELEMETRY_VERSION}] Failed to track error event:`, ex);
            }
        }
    });
    
    // Catch unhandled promise rejections
    window.addEventListener('unhandledrejection', function (event) {
        console.error(`[Telemetry ${TELEMETRY_VERSION}] Unhandled promise rejection:`, event.reason);
        
        if (typeof appInsights !== 'undefined' && appInsights.trackException) {
            try {
                appInsights.trackException({
                    exception: event.reason instanceof Error ? event.reason : new Error(event.reason ? event.reason.toString() : 'Promise rejected'),
                    properties: {
                        type: 'UnhandledRejection',
                        url: window.location.href
                    },
                    severityLevel: 3 // Error
                });
            } catch (ex) {
                console.error(`[Telemetry ${TELEMETRY_VERSION}] Failed to track rejection:`, ex);
            }
        }
    });
    
    console.log(`? [Telemetry ${TELEMETRY_VERSION}] Global error handlers registered`);
};

// Initialize Application Insights (kept for compatibility but simplified)
window.initializeAppInsights = function (instrumentationKey) {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] initializeAppInsights called (SDK loaded in index.html)`);
    // SDK is already loaded in index.html, just verify it's available
    if (typeof appInsights !== 'undefined') {
        console.log(`? [Telemetry ${TELEMETRY_VERSION}] Application Insights SDK detected and ready`);
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] Application Insights SDK not found - it may still be loading`);
    }
};

// Flush pending telemetry events to server
// Critical for mobile networks where events may be buffered
window.flushAppInsights = function () {
    console.log(`[Telemetry ${TELEMETRY_VERSION}] Flushing pending events to Application Insights server...`);
    
    if (typeof appInsights !== 'undefined' && appInsights.flush) {
        try {
            // flush() is async but doesn't return a promise in SDK v3
            appInsights.flush();
            console.log(`? [Telemetry ${TELEMETRY_VERSION}] Flush command sent to Application Insights`);
            
            // Return a promise that resolves after a short delay to ensure flush completes
            return new Promise((resolve) => {
                setTimeout(() => {
                    console.log(`? [Telemetry ${TELEMETRY_VERSION}] Flush operation completed`);
                    resolve();
                }, 1000); // Wait 1 second for flush to complete
            });
        } catch (ex) {
            console.error(`? [Telemetry ${TELEMETRY_VERSION}] Failed to flush events:`, ex);
            return Promise.reject(ex);
        }
    } else {
        console.warn(`?? [Telemetry ${TELEMETRY_VERSION}] appInsights not ready, cannot flush`);
        return Promise.resolve(); // Don't fail if SDK not loaded
    }
};

// Initialize telemetry on page load
console.log(`[Telemetry ${TELEMETRY_VERSION}] Telemetry script loaded at:`, new Date().toLocaleTimeString());
