# TvMazeScraper

- Scrapes the [TVMaze API](https://www.tvmaze.com/api) for show and cast information.
   - Using a background service.
   - Scraping will continue where left off.
   - Scraping can be enabled or disabled with the `ScraperOptions.Enabled` setting in [appsettings.json](TvMazeScraper/appsettings.json).
- Persists the data in storage. 
  - A local sqlite database included in the repository.
  - The database already contains some data, for review purposes.
- Provides the scraped data using a REST API. 
  - URI: `/shows`.
- Provides a paginated list of all tv shows containing the id of the TV show and a list of all the cast that are playing in that TV show.
  - A specific page can be requested by using the `page` query parmeter. For example `/shows?page=1`.
- The list of the cast is ordered by birthday descending.