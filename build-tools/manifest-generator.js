// Environment-specific manifest generation
// This could be part of your build process

export class ManifestGenerator {
    static generateManifest(environment = 'production') {
        const baseManifest = {
            "description": "Play WordScape - Find words in the crossword grid using the circle letters",
            "start_url": "/wordscape",
            "display": "standalone",
            "background_color": "#ffffff",
            "theme_color": "#007bff",
            "orientation": "portrait-primary",
            "scope": "/",
            "icons": [
                {
                    "src": "icon.svg",
                    "sizes": "any",
                    "type": "image/svg+xml",
                    "purpose": "any maskable"
                }
            ],
            "categories": ["games", "entertainment"],
            "shortcuts": [
                {
                    "name": "New WordScape Game",
                    "short_name": "New Game", 
                    "description": "Start a new WordScape puzzle game",
                    "url": "/wordscape",
                    "icons": [
                        {
                            "src": "icon.svg",
                            "sizes": "any",
                            "type": "image/svg+xml"
                        }
                    ]
                }
            ],
            "display_override": ["window-controls-overlay", "standalone", "minimal-ui"],
            "edge_side_panel": {},
            "prefer_related_applications": false
        };

        // Environment-specific customizations
        switch (environment) {
            case 'development':
                return {
                    ...baseManifest,
                    "name": "WordScape (DEV)",
                    "short_name": "WordScape DEV",
                    "version": "1.0.0-dev",
                    "theme_color": "#dc3545", // Red for dev
                    "background_color": "#fff3cd"
                };
                
            case 'staging':
                return {
                    ...baseManifest,
                    "name": "WordScape (STAGING)",
                    "short_name": "WordScape STG",
                    "version": "1.0.0-staging",
                    "theme_color": "#ffc107", // Yellow for staging
                    "background_color": "#fff3cd"
                };
                
            case 'production':
            default:
                return {
                    ...baseManifest,
                    "name": "CalvinHsia WordScape Game",
                    "short_name": "WordScape",
                    "version": "1.0.0",
                    "theme_color": "#007bff", // Blue for production
                    "background_color": "#ffffff"
                };
        }
    }
}

// Usage in build process:
// const manifest = ManifestGenerator.generateManifest(process.env.NODE_ENV);
// fs.writeFileSync('wwwroot/manifest.json', JSON.stringify(manifest, null, 2));