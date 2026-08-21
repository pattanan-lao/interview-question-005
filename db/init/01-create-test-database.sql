-- Runs once, on first initialization of the data volume, as the queue_app
-- role against the queue_system database. The main database is created by
-- the postgres image from POSTGRES_DB; this adds the disposable database
-- the Infrastructure integration tests drop and recreate.
CREATE DATABASE queue_system_test;
