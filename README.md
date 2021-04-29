# PROJECT DESCRIPTION
This project aims to provide an easy-to-use and plug-n-play tool to easily migrate between to DataBases. For now, only SQL Server is supoorted, but the future idea is to support
the most used and most popular DataBases for production, such as POSTGRESQL, MariaDB/MySQL, SQLite and Oracle.

# FEATURES
* **AutoMap:** Easily migrate data between two SQL Servers with little to no configuration at all since the tool searches in both DB for tables and fields 
that are named equally and maps them together for migration.
* **CustomQueries** and **CustomVariables*: You can specify custom queries that will be executed/read on different contexts/times (BeforeMigration, AfterMigration, BeforeTableMigration, AfterTableMigration)
and store them in your CustomVariables for use in later queries OR **EVEN** store them in "System Variables" to use them to alter the logic of the program. For example, as of now, there are 2 SystemVars that
will alter the execution and the way `EasyDataMigrator` handles migrations: %DestTableName and %DestTableStatus. They can be used to obtain the "TableID" and later query the status (whether is busy or not)
of the table and thus wait/retry migration if the table is busy. You can see App.config.sample to see an example on how to do use this.
* **RetryFailed**: Automatically retries failed migrations wheter it was because the tables were busy at the moment or because any non-critical problem that might've happened.
* **Two migration modes**:
	* **Native SQL insert**: For a fast, no memory (or at least, not much) use that will directly migrate data between the 2 servers, using SQL insert into. These mode is the default mode of all migrations
	but I recommend using it only for tables/views that have less than 100.000 records otherwise it takes up to much time.
		* **Required**: For this mode, these requirements must be met:
			* Read access in origin server and read/write acces in destination server.
			* Destination server MUST have origin server as a linked server.
	* **Bulk mode insert**: For a not-so-fast but always reliable migration for tables/views that have more than 100.000 records (I migrated a view that had more than 1.5 M records and it migrated it in a few more than 70 seconds.).
	This mode will be used automatically when native mode fails with a timeout or you can mark tables to use this mode by using `UseBulkCopyTables` setting in App.config.

# FUTURE IDEAS
* As mentioned above, the idea is to support more DataBases such as POSTGRESQL, MariaDB/MySQL, SQLite and Oracle.
* Combine the power of the AutoMap with the user to create custom maps quickly. (Basically AutoMap and save the map, and enable the user to edit the map to add fields/tables that are not named equally).
* Allow different configurations to enable the user to migrate several DB.
* Use hyperthreading to migrate simultaneously several tables or even several configurations.

# APP.CONFIG SETTINGS
* **LogPath**: The path where logs will be stored, can be absoulte or relative.
* **SearchOriginPattern**: A string pattern to filter tables/views that match your pattern. For instance, if your tables start by PERS_
you may use this to filter only the tables that start by PERS_ like this: `<add key="SearchOriginPattern" value="PERS_"/>`
* **SearchDestPattern**: Same as above but for destination server.
* **excludePatternFromMatch**: Pattern string will not be counted when determining that tables are equal, so for instance, if in your origin you have
tables that start by PERS_ but in destination they have no pattern or have another one, both patterns will be ignored when it comes down to match both tables.
* **GetTablesQuery**: This setting is important, it is the query to be used to get the tables or views from both origin and destination servers, by default
I get tables and views, but you can edit it as you please if you have table-valued functions or just tables.
* **UseBulkCopyTables**: As mentioned before, the origin tables put here will use bulk mode for data insert.
* **FailedMigrationsRetries**: Number of retries of failed table migrations (be it because they were busy or for any other error.)
* **WaitTimeBusyTables**: The time (in seconds) we will wait for busy tables before the next retry.
* **MaxQueryTimeout**: The time (in seconds) we will wait for queries to complete, be it for getting, modifying or inserting data.
* **MaxBulkModeTimeout**: Same as above but for bulk mode and for read queries when loading data.
* **MapPrecisionThreshold**: `EasyDataMigrator` calculates the total precision of **Automap** by calculating how many tables in destination
have been matched with origin and same with fields. So you can establish for example a 50%, and that is that you are requiring at least a 50% of tables and fields
to be migrated, otherwise migration will be aborted. Use the value 0 to disable this feature.
* **UseTableControlMechanism**: If you have any table that has flags for determining when a table is being used by another program, set this to true
and use CustomQueries and/or CustomVariables to refill SystemVarible %DestTableIsBusy value. You have an example on how I used it for a real production environment in App.config.sample.


# DISCLAIMER
This software is provided "as is" with absolute no warranty. Use it with precaution and test it in a testing environment before using it in production. 
For now, there are no known bugs but, nonetheless, If you detect a bug, you either can issue an #issue here at github or you can fix it yourself if you want and create a pull request.