# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-01-26

### Added
- Manual financial data input functionality via JSON templates
- Automatic DuPont indicator calculation from basic financial data
- Enhanced data collection workflow with manual data priority
- Data source guidance (CNINFO, Eastmoney, HKEx, etc.)
- `fetch_financial_from_reports.py` script for manual data processing

### Changed
- Updated `analyze_company.py` to integrate manual data collection
- Enhanced analysis context to include DuPont indicators
- Improved error handling for missing data

### Fixed
- Fixed `normalize_stock_code` return value handling (3 values instead of 2)

## [2.0.0] - 2026-01-25

### Added
- Data organization to current working directory
- 5-year DuPont analysis comparison tables
- Automatic risk flagging in financial tables
- Trend indicators (↑↓→) in comparison tables
- Risk annotation rules for 13 key financial indicators

### Changed
- Output directory structure now includes both raw and processed data
- Analysis reports now include formatted comparison tables
- Enhanced analysis prompt with table format requirements

### Improved
- User experience with centralized data management
- Report readability with structured tables
- Risk identification with automatic flagging

## [1.0.0] - 2026-01-24

### Added
- Initial release
- Support for A-shares (Shanghai, Shenzhen, Beijing)
- Support for Hong Kong stocks
- Automated data collection via AkShare API
- DuPont analysis framework
- Business model analysis
- Industry analysis
- Risk assessment
- Investment recommendations
- Comprehensive analysis report generation

### Features
- Multi-market support (A-shares + HK stocks)
- Financial statement collection (5-year history)
- Company announcement collection
- Industry data and peer comparison
- Xueqiu discussion templates
- Professional analysis framework
- Markdown report output

---

## Version History Summary

- **v2.1.0**: Enhanced data collection with manual input support
- **v2.0.0**: Improved reporting with 5-year comparison tables
- **v1.0.0**: Initial release with core analysis features

## Upcoming Features

### Planned for v2.2.0
- [ ] PDF report parsing automation
- [ ] Enhanced data validation logic
- [ ] Multi-source data verification
- [ ] Improved error handling

### Planned for v3.0.0
- [ ] Web UI for data input
- [ ] Real-time data updates
- [ ] Portfolio analysis
- [ ] Automated PDF report generation
- [ ] Chart and visualization generation

---

For detailed information about each version, see the corresponding release notes.
