module.exports = {
  testEnvironment: 'node',
  testMatch: [
    '**/wwwroot/js/**/*.test.js'
  ],
  coverageDirectory: 'coverage',
  collectCoverageFrom: [
    'wwwroot/js/**/*.js',
    '!wwwroot/js/**/*.test.js',
    '!wwwroot/js/**/*.min.js'
  ],
  verbose: true,
  testTimeout: 10000
};
