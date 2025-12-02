// Telemetry JavaScript Functions for Application Insights

// Track a custom event
window.trackEvent = function (eventData) {
    console.log('[Telemetry v2] Tracking event:', eventData.name, 'with', Object.keys(eventData.properties || {}).length, 'properties');
    
    // Use the global appInsights object directly (loaded in index.html)
    if (typeof appInsights !== 'undefined' && appInsights.trackEvent) {
        try {
            appInsights.trackEvent({
                name: eventData.name,
                properties: eventData.properties
            });
            console.log('? [Telemetry v2] Event tracked successfully:', eventData.name);
        } catch (ex) {
            console.error('? [Telemetry v2] Failed to track event:', ex);
        }
    } else {
        console.warn('?? [Telemetry v2] appInsights not ready yet for event:', eventData.name);
    }
};

// Track an exception
window.trackException = function (exceptionData) {
    console.log('[Telemetry v2] Tracking exception:', exceptionData.type);
    
    if (typeof appInsights !== 'undefined' && appInsights.trackException) {
        try {
            appInsights.trackException({
                exception: new Error(exceptionData.message),
                properties: exceptionData.properties,
                severityLevel: 3 // Error
            });
            console.log('? [Telemetry v2] Exception tracked successfully');
        } catch (ex) {
            console.error('? [Telemetry v2] Failed to track exception:', ex);
        }
    } else {
        console.warn('?? [Telemetry v2] appInsights not ready yet');
    }
}

// Track a JavaScript error specifically
window.trackJavaScriptError = function (errorData) {
    console.error('[Telemetry v2] JavaScript error:', errorData.message);
    
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
            console.log('? [Telemetry v2] JS error tracked successfully');
        } catch (ex) {
            console.error('? [Telemetry v2] Failed to track JS error:', ex);
        }
    } else {
        console.warn('?? [Telemetry v2] appInsights not ready yet');
    }
};

// Track a performance metric
window.trackMetric = function (metricData) {
    console.log('[Telemetry v2] Tracking metric:', metricData.name, '=', metricData.value);
    
    if (typeof appInsights !== 'undefined' && appInsights.trackMetric) {
        try {
            appInsights.trackMetric({
                name: metricData.name,
                average: metricData.value,
                properties: metricData.properties
            });
            console.log('? [Telemetry v2] Metric tracked successfully');
        } catch (ex) {
            console.error('? [Telemetry v2] Failed to track metric:', ex);
        }
    } else {
        console.warn('?? [Telemetry v2] appInsights not ready yet');
    }
};

// Setup global error handlers to catch all unhandled errors
window.setupGlobalErrorHandlers = function () {
    console.log('[Telemetry v2] Setting up global error handlers');
    
    // Catch unhandled JavaScript errors
    window.addEventListener('error', function (event) {
        console.error('[Telemetry v2] Uncaught error:', event.error || event.message);
        
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
                console.error('[Telemetry v2] Failed to track error event:', ex);
            }
        }
    });
    
    // Catch unhandled promise rejections
    window.addEventListener('unhandledrejection', function (event) {
        console.error('[Telemetry v2] Unhandled promise rejection:', event.reason);
        
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
                console.error('[Telemetry v2] Failed to track rejection:', ex);
            }
        }
    });
    
    console.log('? [Telemetry v2] Global error handlers registered');
};

// Initialize Application Insights (kept for compatibility but simplified)
window.initializeAppInsights = function (instrumentationKey) {
    console.log('[Telemetry v2] initializeAppInsights called (SDK loaded in index.html)');
    // SDK is already loaded in index.html, just verify it's available
    if (typeof appInsights !== 'undefined') {
        console.log('? [Telemetry v2] Application Insights SDK detected and ready');
    } else {
        console.warn('?? [Telemetry v2] Application Insights SDK not found - it may still be loading');
    }
};

// Initialize telemetry on page load
console.log('[Telemetry v2] Telemetry script loaded at:', new Date().toLocaleTimeString());
