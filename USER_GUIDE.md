BookInfoFinder — User Guide

Version: 1.0
Author: Vo Van Hai (edited and extended for the project)
Purpose: This document guides end users on how to use the BookInfoFinder web application — searching for books, viewing details, favoriting, commenting, rating, and basic management.

I. Overview

1. User Requirements

1.1Actors

| Actor Name            | Role Description                                                                                                                       |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| User                  | Uses the system to search, view details, comment, rate, favorite books, use chatbot, and manage their own account.                     |
| Administrator (Admin) | Manages the entire system: add/edit/delete books, manage authors, publishers, categories, users, view reports, logs, and switch roles. |
| Chatbot               | Automated agent that answers questions, assists users with book information, usage guidance, and book suggestions.                     |
| Email System          | Sends confirmation emails, OTPs, and password reset notifications to users.                                                            |
| Notification System   | Sends realtime notifications to users when relevant events occur (comments, replies, etc.).                                            |

1.2 Use Cases

| ID   | Feature            | Use Case               | Use Case Description                                                                             |
| ---- | ------------------ | ---------------------- | ------------------------------------------------------------------------------------------------ |
| UC1  | User Account       | Login                  | Log in to the system using username and password.                                                |
| UC2  | User Account       | Register               | Register a new account with username, email, and password.                                       |
| UC3  | User Account       | Forgot Password        | Send OTP to email to reset password when forgotten.                                              |
| UC4  | User Account       | Change Password        | Change password when logged in.                                                                  |
| UC5  | User Account       | Edit Profile           | Edit personal information, update avatar.                                                        |
| UC6  | Book Search        | Search Book            | Search for books by title, author, or category.                                                  |
| UC7  | Book Search        | Filter Book            | Filter book list by tag, rating, number of favorites, or trending.                               |
| UC8  | Book Detail        | View Book Detail       | View detailed book information, including description, author, publisher, ratings, and comments. |
| UC9  | Favorites          | Add to Favorites       | Add a book to the user's favorites list.                                                         |
| UC10 | Favorites          | Remove Favorite        | Remove a book from the user's favorites list.                                                    |
| UC11 | Comments/Rating    | Add Comment and Rating | Comment and rate (star) a book (must select a rating when commenting).                           |
| UC12 | Comments           | Edit Comment           | Edit the content of the user's own comment (cannot edit rating).                                 |
| UC13 | Comments/Rating    | Delete Comment         | Delete the user's own comment (and the associated rating).                                       |
| UC14 | Comments/Rating    | View Comments          | View the list of comments and ratings for a book.                                                |
| UC15 | Search History     | View History           | View the user's own search history.                                                              |
| UC16 | Search History     | Delete History         | Delete one or all search history entries.                                                        |
| UC17 | Notification       | View Notification      | Receive and view realtime notifications from the system.                                         |
| UC18 | Chatbot            | Ask Chatbot            | Ask the chatbot for book suggestions, usage guidance, or answers to questions.                   |
| UC19 | Admin - Books      | Add Book               | Admin adds a new book to the system.                                                             |
| UC20 | Admin - Books      | Edit Book              | Admin edits book information.                                                                    |
| UC21 | Admin - Books      | Delete Book            | Admin deletes a book from the system.                                                            |
| UC22 | Admin - Books      | Export to Excel        | Admin exports the book list to an Excel file.                                                    |
| UC23 | Admin - Books      | Export to PDF          | Admin exports the book list or reports to a PDF file.                                            |
| UC24 | Admin - Authors    | Add Author             | Admin adds a new author.                                                                         |
| UC25 | Admin - Authors    | Edit Author            | Admin edits author information.                                                                  |
| UC26 | Admin - Authors    | Delete Author          | Admin deletes an author.                                                                         |
| UC27 | Admin - Publishers | Add Publisher          | Admin adds a new publisher.                                                                      |
| UC28 | Admin - Publishers | Edit Publisher         | Admin edits publisher information.                                                               |
| UC29 | Admin - Publishers | Delete Publisher       | Admin deletes a publisher.                                                                       |
| UC30 | Admin - Categories | Add Category           | Admin adds a new category.                                                                       |
| UC31 | Admin - Categories | Edit Category          | Admin edits category information.                                                                |
| UC32 | Admin - Categories | Delete Category        | Admin deletes a category.                                                                        |
| UC33 | Admin - Tags       | Add Tag                | Admin adds a new tag.                                                                            |
| UC34 | Admin - Tags       | Edit Tag               | Admin edits a tag.                                                                               |
| UC35 | Admin - Tags       | Delete Tag             | Admin deletes a tag.                                                                             |
| UC36 | Admin - Users      | View Users             | Admin views the list of users.                                                                   |
| UC37 | Admin - Users      | Edit User              | Admin edits user information.                                                                    |
| UC38 | Admin - Users      | Disable User           | Admin disables a user account.                                                                   |
| UC39 | Admin - Reports    | View Reports           | Admin views reports, statistics, and system logs.                                                |
| UC40 | Admin - Dashboard  | View All Logs          | Admin views all system activity logs by time.                                                    |
| UC41 | Admin - Dashboard  | View Logs by Date      | Admin filters and views activity logs by date.                                                   |

2. System High Level Design

2.1 Database Design

a. Database Schema

b. Table Descriptions

| No | Table           | Description                                                                                                                 |
| -- | --------------- | --------------------------------------------------------------------------------------------------------------------------- |
| 1  | Users           | Stores user account info. Primary key: UserId. Foreign key: RoleId (references Roles).                                      |
| 2  | Roles           | Stores user roles (User, Admin). Primary key: RoleId.                                                                       |
| 3  | Books           | Stores book info. Primary key: BookId. Foreign keys: AuthorId (Authors), PublisherId (Publishers), CategoryId (Categories). |
| 4  | Authors         | Stores author info. Primary key: AuthorId.                                                                                  |
| 5  | Publishers      | Stores publisher info. Primary key: PublisherId.                                                                            |
| 6  | Categories      | Stores book categories. Primary key: CategoryId.                                                                            |
| 7  | BookComments    | Stores book comments. Primary key: CommentId. Foreign keys: BookId (Books), UserId (Users).                                 |
| 8  | Ratings         | Stores book ratings. Primary key: RatingId. Foreign keys: BookId (Books), UserId (Users).                                   |
| 9  | Favorites       | Stores user favorite books. Primary key: FavoriteId. Foreign keys: BookId (Books), UserId (Users).                          |
| 10 | Tags            | Stores tags for books. Primary key: TagId.                                                                                  |
| 11 | BookTags        | Links books and tags. Primary key: BookTagId. Foreign keys: BookId (Books), TagId (Tags).                                   |
| 12 | SearchHistories | Stores user search history. Primary key: SearchHistoryId. Foreign key: UserId (Users).                                      |
| 13 | Notifications   | Stores notifications for users. Primary key: NotificationId. Foreign key: UserId (Users).                                   |
| 14 | ChatMessages    | Stores chat history with chatbot. Primary key: ChatMessageId. Foreign key: UserId (Users).                                  |

II. Requirement Specifications

| Field             | Description                                                                                                                                                                                                                          |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| UC ID and Name    | UC_1: Login                                                                                                                                                                                                                          |
| Created By        | Vo Van Hai                                                                                                                                                                                                                           |
| Date Created      | 19/11/2025                                                                                                                                                                                                                           |
| Primary Actor     | User                                                                                                                                                                                                                                 |
| Secondary Actors  | None                                                                                                                                                                                                                                 |
| Trigger           | User submits login credentials via the login interface.                                                                                                                                                                              |
| Description       | Allows the user to log in to the system using username and password. The system checks the credentials, and if valid, creates a session and redirects to the main page.                                                              |
| Preconditions     | PRE-1: User has a valid account.`<br>`PRE-2: The system is operational.                                                                                                                                                            |
| Postconditions    | POST-1: User is successfully logged in and a session is created.`<br>`POST-2: If credentials are incorrect, an error is displayed and login is not performed.                                                                      |
| Normal Flow       | 1. User accesses the login page.`<br>`2. Enters username and password.`<br>`3. System searches for user by username.`<br>`4. Compares password (plain text).`<br>`5. If correct, creates session and redirects to main page. |
| Alternative Flows | 1.1 (Incorrect username or password):`<br>`- System displays error message.`<br>`- User retries or selects forgot password.                                                                                                      |
| Exceptions        | 1.2 (System/database error):`<br>`- System displays a system error message.                                                                                                                                                        |
| Priority          | High, Must Have                                                                                                                                                                                                                      |
| Frequency of Use  | Many times per day                                                                                                                                                                                                                   |
| Business Rules    | - Only active users can log in.`<br>`- Password is stored in plain text (per code).                                                                                                                                                |

| Field             | Description                                                                                                                                                                                        |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_2: Register                                                                                                                                                                                     |
| Created By        | Vo Van Hai                                                                                                                                                                                         |
| Date Created      | 19/11/2025                                                                                                                                                                                         |
| Primary Actor     | User                                                                                                                                                                                               |
| Secondary Actors  | None                                                                                                                                                                                               |
| Trigger           | User submits registration form with username, email, and password.                                                                                                                                 |
| Description       | Allows a new user to register an account with username, email, and password. The system validates the information and creates a new account if valid.                                              |
| Preconditions     | PRE-1: User is not logged in.`<br>`PRE-2: The system is operational.                                                                                                                             |
| Postconditions    | POST-1: New user account is created.`<br>`POST-2: If registration fails, an error message is displayed.                                                                                          |
| Normal Flow       | 1. User accesses the registration page.`<br>`2. Enters username, email, and password.`<br>`3. System validates input.`<br>`4. If valid, creates new account and redirects to login page.     |
| Alternative Flows | 3.1 (Username or email already exists):`<br>`- System displays error message.`<br>`- User enters different information.`<br>`3.2 (Validation fails):`<br>`- System displays error message. |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                                                      |
| Priority          | High, Must Have                                                                                                                                                                                    |
| Frequency of Use  | Occasionally, when new users register                                                                                                                                                              |
| Business Rules    | - Username and email must be unique.`<br>`- Required fields must not be empty.                                                                                                                   |

| Field             | Description                                                                                                                                                                                                 |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_3: Forgot Password                                                                                                                                                                                       |
| Created By        | Vo Van Hai                                                                                                                                                                                                  |
| Date Created      | 19/11/2025                                                                                                                                                                                                  |
| Primary Actor     | User                                                                                                                                                                                                        |
| Secondary Actors  | Email System                                                                                                                                                                                                |
| Trigger           | User requests password reset via the forgot password interface.                                                                                                                                             |
| Description       | Allows the user to reset their password by sending an OTP to their registered email. The user enters the OTP and new password to complete the reset.                                                        |
| Preconditions     | PRE-1: User has a registered email.`<br>`PRE-2: The system and email service are operational.                                                                                                             |
| Postconditions    | POST-1: User's password is updated.`<br>`POST-2: If OTP or email is invalid, an error is displayed.                                                                                                       |
| Normal Flow       | 1. User accesses forgot password page.`<br>`2. Enters registered email.`<br>`3. System sends OTP to email.`<br>`4. User enters OTP and new password.`<br>`5. System validates and updates password. |
| Alternative Flows | 3.1 (Email not found):`<br>`- System displays error message.`<br>`4.1 (OTP invalid or expired):`<br>`- System displays error message.                                                                 |
| Exceptions        | 5.1 (System/email error):`<br>`- System displays a system error message.                                                                                                                                  |
| Priority          | High, Must Have                                                                                                                                                                                             |
| Frequency of Use  | Occasionally, when users forget passwords                                                                                                                                                                   |
| Business Rules    | - OTP is required for password reset.`<br>`- OTP expires after a set time.                                                                                                                                |

| Field             | Description                                                                                                                                                                               |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_4: Change Password                                                                                                                                                                     |
| Created By        | Vo Van Hai                                                                                                                                                                                |
| Date Created      | 19/11/2025                                                                                                                                                                                |
| Primary Actor     | User                                                                                                                                                                                      |
| Secondary Actors  | None                                                                                                                                                                                      |
| Trigger           | User submits current and new password via the change password interface.                                                                                                                  |
| Description       | Allows the user to change their password when logged in. The system validates the current password and updates it if valid.                                                               |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.                                                                                                                    |
| Postconditions    | POST-1: User's password is updated.`<br>`POST-2: If validation fails, an error is displayed and password is not changed.                                                                |
| Normal Flow       | 1. User accesses change password page.`<br>`2. Enters current and new password.`<br>`3. System validates current password.`<br>`4. If valid, updates password and confirms success. |
| Alternative Flows | 3.1 (Current password incorrect):`<br>`- System displays error message.`<br>`3.2 (New password invalid):`<br>`- System displays error message.                                      |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                                             |
| Priority          | High, Must Have                                                                                                                                                                           |
| Frequency of Use  | Occasionally, when users want to change passwords                                                                                                                                         |
| Business Rules    | - Only authenticated users can change their password.`<br>`- Password is stored in plain text (per code).                                                                               |

| Field             | Description                                                                                                                                                                |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_5: Edit Profile                                                                                                                                                         |
| Created By        | Vo Van Hai                                                                                                                                                                 |
| Date Created      | 19/11/2025                                                                                                                                                                 |
| Primary Actor     | User                                                                                                                                                                       |
| Secondary Actors  | None                                                                                                                                                                       |
| Trigger           | User submits updated profile information via the edit profile interface.                                                                                                   |
| Description       | Allows the user to edit personal information and update their avatar. The system validates and saves the changes.                                                          |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.                                                                                                     |
| Postconditions    | POST-1: User's profile is updated.`<br>`POST-2: If validation fails, an error is displayed and changes are not saved.                                                    |
| Normal Flow       | 1. User accesses edit profile page.`<br>`2. Updates information and/or avatar.`<br>`3. System validates input.`<br>`4. If valid, saves changes and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- User corrects input and resubmits.                                                                |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                              |
| Priority          | High, Must Have                                                                                                                                                            |
| Frequency of Use  | Occasionally, when users update their profile                                                                                                                              |
| Business Rules    | - Email must be unique.`<br>`- Only the authenticated user can edit their own profile.                                                                                   |

| Field             | Description                                                                                                                               |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_6: Search Book                                                                                                                         |
| Created By        | Vo Van Hai                                                                                                                                |
| Date Created      | 19/11/2025                                                                                                                                |
| Primary Actor     | User                                                                                                                                      |
| Secondary Actors  | None                                                                                                                                      |
| Trigger           | User enters search criteria and submits via the search interface.                                                                         |
| Description       | Allows the user to search for books by title, author, or category. The system returns matching books.                                     |
| Preconditions     | PRE-1: The system is operational.                                                                                                         |
| Postconditions    | POST-1: Search results are displayed.`<br>`POST-2: If no results, a message is shown.                                                   |
| Normal Flow       | 1. User accesses search page.`<br>`2. Enters search criteria.`<br>`3. System searches for matching books.`<br>`4. Displays results. |
| Alternative Flows | 4.1 (No results found):`<br>`- System displays "No books found."                                                                        |
| Exceptions        | 3.1 (System/database error):`<br>`- System displays a system error message.                                                             |
| Priority          | High, Must Have                                                                                                                           |
| Frequency of Use  | Many times per day                                                                                                                        |
| Business Rules    | - Search is case-insensitive.`<br>`- Partial matches are supported.                                                                     |

| Field             | Description                                                                                                                      |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_7: Filter Book                                                                                                                |
| Created By        | Vo Van Hai                                                                                                                       |
| Date Created      | 19/11/2025                                                                                                                       |
| Primary Actor     | User                                                                                                                             |
| Secondary Actors  | None                                                                                                                             |
| Trigger           | User selects filter options and submits via the filter interface.                                                                |
| Description       | Allows the user to filter the book list by tag, rating, number of favorites, or trending. The system displays the filtered list. |
| Preconditions     | PRE-1: The system is operational.                                                                                                |
| Postconditions    | POST-1: Filtered book list is displayed.`<br>`POST-2: If no results, a message is shown.                                       |
| Normal Flow       | 1. User accesses filter options.`<br>`2. Selects filter criteria.`<br>`3. System applies filters and displays results.       |
| Alternative Flows | 3.1 (No results found):`<br>`- System displays "No books found."                                                               |
| Exceptions        | 3.2 (System/database error):`<br>`- System displays a system error message.                                                    |
| Priority          | High, Must Have                                                                                                                  |
| Frequency of Use  | Many times per day                                                                                                               |
| Business Rules    | - Multiple filters can be combined.                                                                                              |

| Field             | Description                                                                                                                 |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_8: View Book Detail                                                                                                      |
| Created By        | Vo Van Hai                                                                                                                  |
| Date Created      | 19/11/2025                                                                                                                  |
| Primary Actor     | User                                                                                                                        |
| Secondary Actors  | None                                                                                                                        |
| Trigger           | User selects a book from the list to view details.                                                                          |
| Description       | Allows the user to view detailed information about a book, including description, author, publisher, ratings, and comments. |
| Preconditions     | PRE-1: The system is operational.                                                                                           |
| Postconditions    | POST-1: Book details are displayed.`<br>`POST-2: If book not found, an error is shown.                                    |
| Normal Flow       | 1. User selects a book.`<br>`2. System retrieves and displays book details.                                               |
| Alternative Flows | 2.1 (Book not found):`<br>`- System displays error message.                                                               |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                                               |
| Priority          | High, Must Have                                                                                                             |
| Frequency of Use  | Many times per day                                                                                                          |
| Business Rules    | - All related information (author, publisher, ratings, comments) is shown.                                                  |

| Field             | Description                                                                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_9: Add to Favorites                                                                                                                        |
| Created By        | Vo Van Hai                                                                                                                                    |
| Date Created      | 19/11/2025                                                                                                                                    |
| Primary Actor     | User                                                                                                                                          |
| Secondary Actors  | None                                                                                                                                          |
| Trigger           | User clicks "Add to Favorites" on a book.                                                                                                     |
| Description       | Allows the user to add a book to their favorites list. The system adds the book if not already in favorites.                                  |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.                                                                        |
| Postconditions    | POST-1: Book is in user's favorites list.`<br>`POST-2: If already in favorites, a message is shown.                                         |
| Normal Flow       | 1. User clicks "Add to Favorites".`<br>`2. System checks if book is already in favorites.`<br>`3. If not, adds book and confirms success. |
| Alternative Flows | 2.1 (Book already in favorites):`<br>`- System displays message.                                                                            |
| Exceptions        | 3.1 (System/database error):`<br>`- System displays a system error message.                                                                 |
| Priority          | High, Must Have                                                                                                                               |
| Frequency of Use  | Many times per day                                                                                                                            |
| Business Rules    | - Each book can only be added once per user.                                                                                                  |

| Field             | Description                                                                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_10: Remove Favorite                                                                                                                        |
| Created By        | Vo Van Hai                                                                                                                                    |
| Date Created      | 19/11/2025                                                                                                                                    |
| Primary Actor     | User                                                                                                                                          |
| Secondary Actors  | None                                                                                                                                          |
| Trigger           | User clicks "Remove from Favorites" on a book.                                                                                                |
| Description       | Allows the user to remove a book from their favorites list. The system removes the book if it exists in favorites.                            |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.                                                                        |
| Postconditions    | POST-1: Book is removed from user's favorites list.`<br>`POST-2: If book not in favorites, a message is shown.                              |
| Normal Flow       | 1. User clicks "Remove from Favorites".`<br>`2. System checks if book is in favorites.`<br>`3. If yes, removes book and confirms success. |
| Alternative Flows | 2.1 (Book not in favorites):`<br>`- System displays message.                                                                                |
| Exceptions        | 3.1 (System/database error):`<br>`- System displays a system error message.                                                                 |
| Priority          | High, Must Have                                                                                                                               |
| Frequency of Use  | Many times per day                                                                                                                            |
| Business Rules    | - Only the user can remove books from their own favorites.                                                                                    |

| Field             | Description                                                                                                                                                                                  |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_11: Add Comment and Rating                                                                                                                                                                |
| Created By        | Vo Van Hai                                                                                                                                                                                   |
| Date Created      | 19/11/2025                                                                                                                                                                                   |
| Primary Actor     | User                                                                                                                                                                                         |
| Secondary Actors  | None                                                                                                                                                                                         |
| Trigger           | User submits a comment and rating for a book via the book detail interface.                                                                                                                  |
| Description       | Allows the user to comment on and rate a book. The user must select a rating when commenting. The system saves both the comment and the rating.                                              |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The user has not already commented on this book.                                                        |
| Postconditions    | POST-1: Comment and rating are saved and displayed.`<br>`POST-2: If validation fails, an error is shown and nothing is saved.                                                              |
| Normal Flow       | 1. User accesses book detail page.`<br>`2. Enters comment and selects rating.`<br>`3. Submits form.`<br>`4. System validates input.`<br>`5. If valid, saves comment and rating.      |
| Alternative Flows | 4.1 (Validation fails):`<br>`- System displays error message.`<br>`- User corrects input and resubmits.`<br>`4.2 (User already commented):`<br>`- System prevents duplicate comment. |
| Exceptions        | 5.1 (System/database error):`<br>`- System displays a system error message.                                                                                                                |
| Priority          | High, Must Have                                                                                                                                                                              |
| Frequency of Use  | Many times per day                                                                                                                                                                           |
| Business Rules    | - User must select a rating when commenting.`<br>`- Each user can comment only once per book.`<br>`- Comment and rating are linked and saved together.                                   |

| Field             | Description                                                                                                                                                                                  |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_12: Edit Comment                                                                                                                                                                          |
| Created By        | Vo Van Hai                                                                                                                                                                                   |
| Date Created      | 19/11/2025                                                                                                                                                                                   |
| Primary Actor     | User                                                                                                                                                                                         |
| Secondary Actors  | None                                                                                                                                                                                         |
| Trigger           | User selects to edit their own comment via the book detail interface.                                                                                                                        |
| Description       | Allows the user to edit the content of their own comment on a book. The rating cannot be edited.                                                                                             |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The user has previously commented on the book.                                                          |
| Postconditions    | POST-1: Comment content is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                                         |
| Normal Flow       | 1. User accesses their comment on a book.`<br>`2. Selects edit option.`<br>`3. Updates comment content.`<br>`4. Submits changes.`<br>`5. System validates and saves the new content. |
| Alternative Flows | 5.1 (Validation fails):`<br>`- System displays error message.`<br>`- User corrects input and resubmits.                                                                                  |
| Exceptions        | 5.2 (System/database error):`<br>`- System displays a system error message.                                                                                                                |
| Priority          | Medium, Should Have                                                                                                                                                                          |
| Frequency of Use  | Occasionally, when users want to update their comments                                                                                                                                       |
| Business Rules    | - Only the comment content can be edited.`<br>`- Rating cannot be changed after initial submission.`<br>`- Only the comment owner can edit their comment.                                |

| Field             | Description                                                                                                                                                        |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| UC ID and Name    | UC_13: Delete Comment                                                                                                                                              |
| Created By        | Vo Van Hai                                                                                                                                                         |
| Date Created      | 19/11/2025                                                                                                                                                         |
| Primary Actor     | User                                                                                                                                                               |
| Secondary Actors  | None                                                                                                                                                               |
| Trigger           | User selects to delete their own comment via the book detail interface.                                                                                            |
| Description       | Allows the user to delete their own comment on a book. The associated rating is also deleted.                                                                      |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The user has previously commented on the book.                                |
| Postconditions    | POST-1: Comment and associated rating are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                     |
| Normal Flow       | 1. User accesses their comment on a book.`<br>`2. Selects delete option.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes comment and rating. |
| Alternative Flows | 3.1 (User cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Comment not found):`<br>`- System displays error message.                                  |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                      |
| Priority          | Medium, Should Have                                                                                                                                                |
| Frequency of Use  | Occasionally, when users want to remove their comments                                                                                                             |
| Business Rules    | - Deleting a comment also deletes the associated rating.`<br>`- Only the comment owner can delete their comment.                                                 |

| Field             | Description                                                                                                       |
| ----------------- | ----------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_14: View Comments                                                                                              |
| Created By        | Vo Van Hai                                                                                                        |
| Date Created      | 19/11/2025                                                                                                        |
| Primary Actor     | User                                                                                                              |
| Secondary Actors  | None                                                                                                              |
| Trigger           | User views the comments section on a book detail page.                                                            |
| Description       | Allows the user to view the list of comments and ratings for a book.                                              |
| Preconditions     | PRE-1: The system is operational.`<br>`PRE-2: The book exists in the system.                                    |
| Postconditions    | POST-1: List of comments and ratings is displayed.`<br>`POST-2: If no comments, a message is shown.             |
| Normal Flow       | 1. User accesses book detail page.`<br>`2. System retrieves and displays all comments and ratings for the book. |
| Alternative Flows | 2.1 (No comments found):`<br>`- System displays "No comments yet."                                              |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                                     |
| Priority          | High, Must Have                                                                                                   |
| Frequency of Use  | Many times per day                                                                                                |
| Business Rules    | - Comments and ratings are displayed in chronological order.`<br>`- All users can view comments and ratings.    |

| Field             | Description                                                                                          |
| ----------------- | ---------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_15: View History                                                                                  |
| Created By        | Vo Van Hai                                                                                           |
| Date Created      | 19/11/2025                                                                                           |
| Primary Actor     | User                                                                                                 |
| Secondary Actors  | None                                                                                                 |
| Trigger           | User accesses the search history page.                                                               |
| Description       | Allows the user to view their own search history.                                                    |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.                               |
| Postconditions    | POST-1: User's search history is displayed.`<br>`POST-2: If no history, a message is shown.        |
| Normal Flow       | 1. User accesses search history page.`<br>`2. System retrieves and displays user's search history. |
| Alternative Flows | 2.1 (No history found):`<br>`- System displays "No search history."                                |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                        |
| Priority          | Medium, Should Have                                                                                  |
| Frequency of Use  | Occasionally, when users want to review their searches                                               |
| Business Rules    | - Only the authenticated user can view their own search history.                                     |

| Field             | Description                                                                                                                                                                      |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_16: Delete History                                                                                                                                                            |
| Created By        | Vo Van Hai                                                                                                                                                                       |
| Date Created      | 19/11/2025                                                                                                                                                                       |
| Primary Actor     | User                                                                                                                                                                             |
| Secondary Actors  | None                                                                                                                                                                             |
| Trigger           | User selects to delete one or all search history entries.                                                                                                                        |
| Description       | Allows the user to delete one or all entries from their search history.                                                                                                          |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: User has search history.                                                                    |
| Postconditions    | POST-1: Selected search history entries are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                                 |
| Normal Flow       | 1. User accesses search history page.`<br>`2. Selects entries to delete or "delete all".`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes selected entries. |
| Alternative Flows | 3.1 (User cancels deletion):`<br>`- No action is taken.`<br>`4.1 (No entries selected):`<br>`- System displays message.                                                    |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                    |
| Priority          | Medium, Should Have                                                                                                                                                              |
| Frequency of Use  | Occasionally, when users want to clear their search history                                                                                                                      |
| Business Rules    | - Only the authenticated user can delete their own search history.                                                                                                               |

| Field             | Description                                                                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_17: View Notification                                                                                                                    |
| Created By        | Vo Van Hai                                                                                                                                  |
| Date Created      | 19/11/2025                                                                                                                                  |
| Primary Actor     | User                                                                                                                                        |
| Secondary Actors  | Notification System                                                                                                                         |
| Trigger           | System sends a notification or user accesses the notification page.                                                                         |
| Description       | Allows the user to receive and view real-time notifications from the system.                                                                |
| Preconditions     | PRE-1: User is authenticated.`<br>`PRE-2: The system and notification service are operational.                                            |
| Postconditions    | POST-1: Notifications are displayed to the user.`<br>`POST-2: If no notifications, a message is shown.                                    |
| Normal Flow       | 1. System sends notification to user.`<br>`2. User accesses notification page or receives popup.`<br>`3. System displays notifications. |
| Alternative Flows | 3.1 (No notifications):`<br>`- System displays "No notifications."                                                                        |
| Exceptions        | 3.2 (System/notification error):`<br>`- System displays a system error message.                                                           |
| Priority          | High, Must Have                                                                                                                             |
| Frequency of Use  | Many times per day                                                                                                                          |
| Business Rules    | - Only authenticated users receive notifications.`<br>`- Notifications are delivered in real time.                                        |

| Field             | Description                                                                                                                                                                                           |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_18: Ask Chatbot                                                                                                                                                                                    |
| Created By        | Vo Van Hai                                                                                                                                                                                            |
| Date Created      | 19/11/2025                                                                                                                                                                                            |
| Primary Actor     | User                                                                                                                                                                                                  |
| Secondary Actors  | Chatbot                                                                                                                                                                                               |
| Trigger           | User submits a question or request to the chatbot via the chat interface.                                                                                                                             |
| Description       | Allows the user to ask the chatbot for book suggestions, usage guidance, or answers to questions. The chatbot responds in real time.                                                                  |
| Preconditions     | PRE-1: The system and chatbot service are operational.                                                                                                                                                |
| Postconditions    | POST-1: Chatbot response is displayed to the user.`<br>`POST-2: If chatbot cannot answer, a message is shown.                                                                                       |
| Normal Flow       | 1. User accesses chat interface.`<br>`2. Enters question or request.`<br>`3. System sends input to chatbot.`<br>`4. Chatbot processes and returns response.`<br>`5. System displays response. |
| Alternative Flows | 4.1 (Chatbot cannot answer):`<br>`- System displays "Sorry, I don't have an answer."                                                                                                                |
| Exceptions        | 4.2 (System/chatbot error):`<br>`- System displays a system error message.                                                                                                                          |
| Priority          | Medium, Should Have                                                                                                                                                                                   |
| Frequency of Use  | Occasionally, when users need help or suggestions                                                                                                                                                     |
| Business Rules    | - Chatbot responses are generated in real time.`<br>`- User input is logged for quality improvement.                                                                                                |

| Field             | Description                                                                                                                                                |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_19: Add Book                                                                                                                                            |
| Created By        | Vo Van Hai                                                                                                                                                 |
| Date Created      | 19/11/2025                                                                                                                                                 |
| Primary Actor     | Admin                                                                                                                                                      |
| Secondary Actors  | None                                                                                                                                                       |
| Trigger           | Admin submits new book information via the add book interface.                                                                                             |
| Description       | Allows the admin to add a new book to the system. The system validates and saves the book information.                                                     |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                                                    |
| Postconditions    | POST-1: New book is added to the system.`<br>`POST-2: If validation fails, an error is shown and book is not added.                                      |
| Normal Flow       | 1. Admin accesses add book page.`<br>`2. Enters book information.`<br>`3. System validates input.`<br>`4. If valid, saves book and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.                                               |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                              |
| Priority          | High, Must Have                                                                                                                                            |
| Frequency of Use  | Occasionally, when new books are added                                                                                                                     |
| Business Rules    | - Only admins can add books.`<br>`- Book title must be unique.                                                                                           |

| Field             | Description                                                                                                                                                                       |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_20: Edit Book                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                        |
| Date Created      | 19/11/2025                                                                                                                                                                        |
| Primary Actor     | Admin                                                                                                                                                                             |
| Secondary Actors  | None                                                                                                                                                                              |
| Trigger           | Admin selects a book and submits updated information via the edit book interface.                                                                                                 |
| Description       | Allows the admin to edit information for an existing book. The system validates and updates the book information.                                                                 |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The book exists in the system.                                                              |
| Postconditions    | POST-1: Book information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit book page.`<br>`2. Updates book information.`<br>`3. System validates input.`<br>`4. If valid, updates book and confirms success.                    |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (Book not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                     |
| Priority          | High, Must Have                                                                                                                                                                   |
| Frequency of Use  | Occasionally, when book information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit books.`<br>`- Book title must remain unique.                                                                                                             |

| Field             | Description                                                                                                                                                            |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_21: Delete Book                                                                                                                                                     |
| Created By        | Vo Van Hai                                                                                                                                                             |
| Date Created      | 19/11/2025                                                                                                                                                             |
| Primary Actor     | Admin                                                                                                                                                                  |
| Secondary Actors  | None                                                                                                                                                                   |
| Trigger           | Admin selects a book and chooses to delete it via the book management interface.                                                                                       |
| Description       | Allows the admin to delete a book from the system. The system removes the book and all related data (e.g., comments, ratings, favorites).                              |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The book exists in the system.                                                   |
| Postconditions    | POST-1: Book and related data are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                                 |
| Normal Flow       | 1. Admin accesses book management page.`<br>`2. Selects a book to delete.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes book and related data. |
| Alternative Flows | 3.1 (Admin cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Book not found):`<br>`- System displays error message.                                        |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                          |
| Priority          | High, Must Have                                                                                                                                                        |
| Frequency of Use  | Occasionally, when books need to be removed                                                                                                                            |
| Business Rules    | - Only admins can delete books.`<br>`- Deleting a book removes all related comments, ratings, and favorites.                                                         |

| Field             | Description                                                                                                                                              |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_22: Export to Excel                                                                                                                                   |
| Created By        | Vo Van Hai                                                                                                                                               |
| Date Created      | 19/11/2025                                                                                                                                               |
| Primary Actor     | Admin                                                                                                                                                    |
| Secondary Actors  | None                                                                                                                                                     |
| Trigger           | Admin selects the option to export the book list to Excel via the export interface.                                                                      |
| Description       | Allows the admin to export the book list to an Excel file. The system generates and provides the file for download.                                      |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: There are books in the system.                                     |
| Postconditions    | POST-1: Excel file is generated and available for download.`<br>`POST-2: If export fails, an error is shown and no file is generated.                  |
| Normal Flow       | 1. Admin accesses export interface.`<br>`2. Selects "Export to Excel".`<br>`3. System generates Excel file.`<br>`4. File is provided for download. |
| Alternative Flows | 3.1 (No books to export):`<br>`- System displays message.`<br>`4.1 (Admin cancels export):`<br>`- No action is taken.                              |
| Exceptions        | 4.2 (System/export error):`<br>`- System displays a system error message.                                                                              |
| Priority          | Medium, Should Have                                                                                                                                      |
| Frequency of Use  | Occasionally, for reporting or backup                                                                                                                    |
| Business Rules    | - Only admins can export data.`<br>`- Exported file must follow Excel format standards.                                                                |

| Field             | Description                                                                                                                                          |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_23: Export to PDF                                                                                                                                 |
| Created By        | Vo Van Hai                                                                                                                                           |
| Date Created      | 19/11/2025                                                                                                                                           |
| Primary Actor     | Admin                                                                                                                                                |
| Secondary Actors  | None                                                                                                                                                 |
| Trigger           | Admin selects the option to export the book list or reports to PDF via the export interface.                                                         |
| Description       | Allows the admin to export the book list or reports to a PDF file. The system generates and provides the file for download.                          |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: There is data to export.                                       |
| Postconditions    | POST-1: PDF file is generated and available for download.`<br>`POST-2: If export fails, an error is shown and no file is generated.                |
| Normal Flow       | 1. Admin accesses export interface.`<br>`2. Selects "Export to PDF".`<br>`3. System generates PDF file.`<br>`4. File is provided for download. |
| Alternative Flows | 3.1 (No data to export):`<br>`- System displays message.`<br>`4.1 (Admin cancels export):`<br>`- No action is taken.                           |
| Exceptions        | 4.2 (System/export error):`<br>`- System displays a system error message.                                                                          |
| Priority          | Medium, Should Have                                                                                                                                  |
| Frequency of Use  | Occasionally, for reporting or backup                                                                                                                |
| Business Rules    | - Only admins can export data.`<br>`- Exported file must follow PDF format standards.                                                              |

| Field             | Description                                                                                                                                                      |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_24: Add Author                                                                                                                                                |
| Created By        | Vo Van Hai                                                                                                                                                       |
| Date Created      | 19/11/2025                                                                                                                                                       |
| Primary Actor     | Admin                                                                                                                                                            |
| Secondary Actors  | None                                                                                                                                                             |
| Trigger           | Admin submits new author information via the add author interface.                                                                                               |
| Description       | Allows the admin to add a new author to the system. The system validates and saves the author information.                                                       |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                                                          |
| Postconditions    | POST-1: New author is added to the system.`<br>`POST-2: If validation fails, an error is shown and author is not added.                                        |
| Normal Flow       | 1. Admin accesses add author page.`<br>`2. Enters author information.`<br>`3. System validates input.`<br>`4. If valid, saves author and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.                                                     |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                    |
| Priority          | Medium, Should Have                                                                                                                                              |
| Frequency of Use  | Occasionally, when new authors are added                                                                                                                         |
| Business Rules    | - Only admins can add authors.`<br>`- Author name must be unique.                                                                                              |

| Field             | Description                                                                                                                                                                         |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_25: Edit Author                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                          |
| Date Created      | 19/11/2025                                                                                                                                                                          |
| Primary Actor     | Admin                                                                                                                                                                               |
| Secondary Actors  | None                                                                                                                                                                                |
| Trigger           | Admin selects an author and submits updated information via the edit author interface.                                                                                              |
| Description       | Allows the admin to edit information for an existing author. The system validates and updates the author information.                                                               |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The author exists in the system.                                                              |
| Postconditions    | POST-1: Author information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit author page.`<br>`2. Updates author information.`<br>`3. System validates input.`<br>`4. If valid, updates author and confirms success.                |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (Author not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                       |
| Priority          | Medium, Should Have                                                                                                                                                                 |
| Frequency of Use  | Occasionally, when author information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit authors.`<br>`- Author name must remain unique.                                                                                                            |

| Field             | Description                                                                                                                                                                   |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_26: Delete Author                                                                                                                                                          |
| Created By        | Vo Van Hai                                                                                                                                                                    |
| Date Created      | 19/11/2025                                                                                                                                                                    |
| Primary Actor     | Admin                                                                                                                                                                         |
| Secondary Actors  | None                                                                                                                                                                          |
| Trigger           | Admin selects an author and chooses to delete via the author management interface.                                                                                            |
| Description       | Allows the admin to delete an author from the system. The system removes the author and all related data (e.g., books, references).                                           |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The author exists in the system.                                                        |
| Postconditions    | POST-1: Author and related data are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                                      |
| Normal Flow       | 1. Admin accesses author management page.`<br>`2. Selects an author to delete.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes author and related data. |
| Alternative Flows | 3.1 (Admin cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Author not found):`<br>`- System displays error message.                                             |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                 |
| Priority          | Medium, Should Have                                                                                                                                                           |
| Frequency of Use  | Occasionally, when authors need to be removed                                                                                                                                 |
| Business Rules    | - Only admins can delete authors.`<br>`- Deleting an author removes all related references.                                                                                 |

| Field             | Description                                                                                                                                                               |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_27: Add Publisher                                                                                                                                                      |
| Created By        | Vo Van Hai                                                                                                                                                                |
| Date Created      | 19/11/2025                                                                                                                                                                |
| Primary Actor     | Admin                                                                                                                                                                     |
| Secondary Actors  | None                                                                                                                                                                      |
| Trigger           | Admin submits new publisher information via the add publisher interface.                                                                                                  |
| Description       | Allows the admin to add a new publisher to the system. The system validates and saves the publisher information.                                                          |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                                                                   |
| Postconditions    | POST-1: New publisher is added to the system.`<br>`POST-2: If validation fails, an error is shown and publisher is not added.                                           |
| Normal Flow       | 1. Admin accesses add publisher page.`<br>`2. Enters publisher information.`<br>`3. System validates input.`<br>`4. If valid, saves publisher and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.                                                              |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                             |
| Priority          | Medium, Should Have                                                                                                                                                       |
| Frequency of Use  | Occasionally, when new publishers are added                                                                                                                               |
| Business Rules    | - Only admins can add publishers.`<br>`- Publisher name must be unique.                                                                                                 |

| Field             | Description                                                                                                                                                                            |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_28: Edit Publisher                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                             |
| Date Created      | 19/11/2025                                                                                                                                                                             |
| Primary Actor     | Admin                                                                                                                                                                                  |
| Secondary Actors  | None                                                                                                                                                                                   |
| Trigger           | Admin selects a publisher and submits updated information via the edit publisher interface.                                                                                            |
| Description       | Allows the admin to edit information for an existing publisher. The system validates and updates the publisher information.                                                            |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The publisher exists in the system.                                                              |
| Postconditions    | POST-1: Publisher information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit publisher page.`<br>`2. Updates publisher information.`<br>`3. System validates input.`<br>`4. If valid, updates publisher and confirms success.          |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (Publisher not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                          |
| Priority          | Medium, Should Have                                                                                                                                                                    |
| Frequency of Use  | Occasionally, when publisher information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit publishers.`<br>`- Publisher name must remain unique.                                                                                                         |

| Field             | Description                                                                                                                                                                           |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_29: Delete Publisher                                                                                                                                                               |
| Created By        | Vo Van Hai                                                                                                                                                                            |
| Date Created      | 19/11/2025                                                                                                                                                                            |
| Primary Actor     | Admin                                                                                                                                                                                 |
| Secondary Actors  | None                                                                                                                                                                                  |
| Trigger           | Admin selects a publisher and chooses to delete via the publisher management interface.                                                                                               |
| Description       | Allows the admin to delete a publisher from the system. The system removes the publisher and all related data (e.g., books, references).                                              |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The publisher exists in the system.                                                             |
| Postconditions    | POST-1: Publisher and related data are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                                           |
| Normal Flow       | 1. Admin accesses publisher management page.`<br>`2. Selects a publisher to delete.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes publisher and related data. |
| Alternative Flows | 3.1 (Admin cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Publisher not found):`<br>`- System displays error message.                                                  |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                         |
| Priority          | Medium, Should Have                                                                                                                                                                   |
| Frequency of Use  | Occasionally, when publishers need to be removed                                                                                                                                      |
| Business Rules    | - Only admins can delete publishers.`<br>`- Deleting a publisher removes all related references.                                                                                    |

| Field             | Description                                                                                                                                                            |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_30: Add Category                                                                                                                                                    |
| Created By        | Vo Van Hai                                                                                                                                                             |
| Date Created      | 19/11/2025                                                                                                                                                             |
| Primary Actor     | Admin                                                                                                                                                                  |
| Secondary Actors  | None                                                                                                                                                                   |
| Trigger           | Admin submits new category information via the add category interface.                                                                                                 |
| Description       | Allows the admin to add a new category to the system. The system validates and saves the category information.                                                         |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                                                                |
| Postconditions    | POST-1: New category is added to the system.`<br>`POST-2: If validation fails, an error is shown and category is not added.                                          |
| Normal Flow       | 1. Admin accesses add category page.`<br>`2. Enters category information.`<br>`3. System validates input.`<br>`4. If valid, saves category and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.                                                           |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                                          |
| Priority          | Medium, Should Have                                                                                                                                                    |
| Frequency of Use  | Occasionally, when new categories are added                                                                                                                            |
| Business Rules    | - Only admins can add categories.`<br>`- Category name must be unique.                                                                                               |

| Field             | Description                                                                                                                                                                           |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_31: Edit Category                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                            |
| Date Created      | 19/11/2025                                                                                                                                                                            |
| Primary Actor     | Admin                                                                                                                                                                                 |
| Secondary Actors  | None                                                                                                                                                                                  |
| Trigger           | Admin selects a category and submits updated information via the edit category interface.                                                                                             |
| Description       | Allows the admin to edit information for an existing category. The system validates and updates the category information.                                                             |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The category exists in the system.                                                              |
| Postconditions    | POST-1: Category information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit category page.`<br>`2. Updates category information.`<br>`3. System validates input.`<br>`4. If valid, updates category and confirms success.            |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (Category not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                         |
| Priority          | Medium, Should Have                                                                                                                                                                   |
| Frequency of Use  | Occasionally, when category information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit categories.`<br>`- Category name must remain unique.                                                                                                         |

| Field             | Description                                                                                                                                                                        |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_32: Delete Category                                                                                                                                                             |
| Created By        | Vo Van Hai                                                                                                                                                                         |
| Date Created      | 19/11/2025                                                                                                                                                                         |
| Primary Actor     | Admin                                                                                                                                                                              |
| Secondary Actors  | None                                                                                                                                                                               |
| Trigger           | Admin selects a category and chooses to delete via the category management interface.                                                                                              |
| Description       | Allows the admin to delete a category from the system. The system removes the category and all related data (e.g., books, references).                                             |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The category exists in the system.                                                           |
| Postconditions    | POST-1: Category and related data are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                                         |
| Normal Flow       | 1. Admin accesses category management page.`<br>`2. Selects a category to delete.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes category and related data. |
| Alternative Flows | 3.1 (Admin cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Category not found):`<br>`- System displays error message.                                                |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                      |
| Priority          | Medium, Should Have                                                                                                                                                                |
| Frequency of Use  | Occasionally, when categories need to be removed                                                                                                                                   |
| Business Rules    | - Only admins can delete categories.`<br>`- Deleting a category removes all related references.                                                                                  |

| Field             | Description                                                                                                                                             |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_33: Add Tag                                                                                                                                          |
| Created By        | Vo Van Hai                                                                                                                                              |
| Date Created      | 19/11/2025                                                                                                                                              |
| Primary Actor     | Admin                                                                                                                                                   |
| Secondary Actors  | None                                                                                                                                                    |
| Trigger           | Admin submits new tag information via the add tag interface.                                                                                            |
| Description       | Allows the admin to add a new tag to the system. The system validates and saves the tag information.                                                    |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                                                 |
| Postconditions    | POST-1: New tag is added to the system.`<br>`POST-2: If validation fails, an error is shown and tag is not added.                                     |
| Normal Flow       | 1. Admin accesses add tag page.`<br>`2. Enters tag information.`<br>`3. System validates input.`<br>`4. If valid, saves tag and confirms success. |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.                                            |
| Exceptions        | 4.1 (System/database error):`<br>`- System displays a system error message.                                                                           |
| Priority          | Medium, Should Have                                                                                                                                     |
| Frequency of Use  | Occasionally, when new tags are added                                                                                                                   |
| Business Rules    | - Only admins can add tags.`<br>`- Tag name must be unique.                                                                                           |

| Field             | Description                                                                                                                                                                      |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_34: Edit Tag                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                       |
| Date Created      | 19/11/2025                                                                                                                                                                       |
| Primary Actor     | Admin                                                                                                                                                                            |
| Secondary Actors  | None                                                                                                                                                                             |
| Trigger           | Admin selects a tag and submits updated information via the edit tag interface.                                                                                                  |
| Description       | Allows the admin to edit information for an existing tag. The system validates and updates the tag information.                                                                  |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The tag exists in the system.                                                              |
| Postconditions    | POST-1: Tag information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit tag page.`<br>`2. Updates tag information.`<br>`3. System validates input.`<br>`4. If valid, updates tag and confirms success.                      |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (Tag not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                    |
| Priority          | Medium, Should Have                                                                                                                                                              |
| Frequency of Use  | Occasionally, when tag information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit tags.`<br>`- Tag name must remain unique.                                                                                                               |

| Field             | Description                                                                                                                                                         |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_35: Delete Tag                                                                                                                                                   |
| Created By        | Vo Van Hai                                                                                                                                                          |
| Date Created      | 19/11/2025                                                                                                                                                          |
| Primary Actor     | Admin                                                                                                                                                               |
| Secondary Actors  | None                                                                                                                                                                |
| Trigger           | Admin selects a tag and chooses to delete via the tag management interface.                                                                                         |
| Description       | Allows the admin to delete a tag from the system. The system removes the tag and all related data (e.g., book-tag associations).                                    |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The tag exists in the system.                                                 |
| Postconditions    | POST-1: Tag and related data are deleted.`<br>`POST-2: If deletion fails, an error is shown and nothing is deleted.                                               |
| Normal Flow       | 1. Admin accesses tag management page.`<br>`2. Selects a tag to delete.`<br>`3. System confirms deletion.`<br>`4. If confirmed, deletes tag and related data. |
| Alternative Flows | 3.1 (Admin cancels deletion):`<br>`- No action is taken.`<br>`4.1 (Tag not found):`<br>`- System displays error message.                                      |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                       |
| Priority          | Medium, Should Have                                                                                                                                                 |
| Frequency of Use  | Occasionally, when tags need to be removed                                                                                                                          |
| Business Rules    | - Only admins can delete tags.`<br>`- Deleting a tag removes all related associations.                                                                            |

| Field             | Description                                                                                                                         |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_36: View Users                                                                                                                   |
| Created By        | Vo Van Hai                                                                                                                          |
| Date Created      | 19/11/2025                                                                                                                          |
| Primary Actor     | Admin                                                                                                                               |
| Secondary Actors  | None                                                                                                                                |
| Trigger           | Admin accesses the user management page.                                                                                            |
| Description       | Allows the admin to view the list of users in the system. The system displays user information with options for further management. |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                                             |
| Postconditions    | POST-1: List of users is displayed.`<br>`POST-2: If no users, a message is shown.                                                 |
| Normal Flow       | 1. Admin accesses user management page.`<br>`2. System retrieves and displays list of users.                                      |
| Alternative Flows | 2.1 (No users found):`<br>`- System displays "No users found."                                                                    |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                                                       |
| Priority          | High, Must Have                                                                                                                     |
| Frequency of Use  | Occasionally, for user management                                                                                                   |
| Business Rules    | - Only admins can view the user list.`<br>`- User information is displayed with management options.                               |

| Field             | Description                                                                                                                                                                       |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_37: Edit User                                                                                                                                                                  |
| Created By        | Vo Van Hai                                                                                                                                                                        |
| Date Created      | 19/11/2025                                                                                                                                                                        |
| Primary Actor     | Admin                                                                                                                                                                             |
| Secondary Actors  | None                                                                                                                                                                              |
| Trigger           | Admin selects a user and submits updated information via the edit user interface.                                                                                                 |
| Description       | Allows the admin to edit information for an existing user. The system validates and updates the user information.                                                                 |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The user exists in the system.                                                              |
| Postconditions    | POST-1: User information is updated.`<br>`POST-2: If validation fails, an error is shown and changes are not saved.                                                             |
| Normal Flow       | 1. Admin accesses edit user page.`<br>`2. Updates user information.`<br>`3. System validates input.`<br>`4. If valid, updates user and confirms success.                    |
| Alternative Flows | 3.1 (Validation fails):`<br>`- System displays error message.`<br>`- Admin corrects input and resubmits.`<br>`4.1 (User not found):`<br>`- System displays error message. |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                                     |
| Priority          | High, Must Have                                                                                                                                                                   |
| Frequency of Use  | Occasionally, when user information needs updating                                                                                                                                |
| Business Rules    | - Only admins can edit users.`<br>`- Email and username must remain unique.                                                                                                     |

| Field             | Description                                                                                                                                                   |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_38: Disable User                                                                                                                                           |
| Created By        | Vo Van Hai                                                                                                                                                    |
| Date Created      | 19/11/2025                                                                                                                                                    |
| Primary Actor     | Admin                                                                                                                                                         |
| Secondary Actors  | None                                                                                                                                                          |
| Trigger           | Admin selects a user and chooses to disable the account via the user management interface.                                                                    |
| Description       | Allows the admin to disable a user account, preventing the user from logging in. The system updates the user's status to inactive.                            |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: The user exists in the system.                                          |
| Postconditions    | POST-1: User account is disabled.`<br>`POST-2: If operation fails, an error is shown and status is not changed.                                             |
| Normal Flow       | 1. Admin accesses user management page.`<br>`2. Selects a user to disable.`<br>`3. System confirms action.`<br>`4. If confirmed, disables user account. |
| Alternative Flows | 3.1 (Admin cancels action):`<br>`- No action is taken.`<br>`4.1 (User not found):`<br>`- System displays error message.                                 |
| Exceptions        | 4.2 (System/database error):`<br>`- System displays a system error message.                                                                                 |
| Priority          | High, Must Have                                                                                                                                               |
| Frequency of Use  | Occasionally, for user management                                                                                                                             |
| Business Rules    | - Only admins can disable users.`<br>`- Disabled users cannot log in.                                                                                       |

| Field             | Description                                                                                                       |
| ----------------- | ----------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_39: View Reports                                                                                               |
| Created By        | Vo Van Hai                                                                                                        |
| Date Created      | 19/11/2025                                                                                                        |
| Primary Actor     | Admin                                                                                                             |
| Secondary Actors  | None                                                                                                              |
| Trigger           | Admin accesses the reports or statistics page.                                                                    |
| Description       | Allows the admin to view reports, statistics, and system logs. The system displays relevant data and analytics.   |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                           |
| Postconditions    | POST-1: Reports and statistics are displayed.`<br>`POST-2: If no data, a message is shown.                      |
| Normal Flow       | 1. Admin accesses reports/statistics page.`<br>`2. System retrieves and displays reports, statistics, and logs. |
| Alternative Flows | 2.1 (No data found):`<br>`- System displays "No data available."                                                |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                                     |
| Priority          | Medium, Should Have                                                                                               |
| Frequency of Use  | Occasionally, for system monitoring and analysis                                                                  |
| Business Rules    | - Only admins can view reports and statistics.`<br>`- Data is displayed in a readable format.                   |

| Field             | Description                                                                                                  |
| ----------------- | ------------------------------------------------------------------------------------------------------------ |
| UC ID and Name    | UC_40: View All Logs                                                                                         |
| Created By        | Vo Van Hai                                                                                                   |
| Date Created      | 19/11/2025                                                                                                   |
| Primary Actor     | Admin                                                                                                        |
| Secondary Actors  | None                                                                                                         |
| Trigger           | Admin accesses the system logs page.                                                                         |
| Description       | Allows the admin to view all system activity logs by time. The system displays logs in chronological order.  |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.                                      |
| Postconditions    | POST-1: System logs are displayed.`<br>`POST-2: If no logs, a message is shown.                            |
| Normal Flow       | 1. Admin accesses system logs page.`<br>`2. System retrieves and displays all logs in chronological order. |
| Alternative Flows | 2.1 (No logs found):`<br>`- System displays "No logs available."                                           |
| Exceptions        | 2.2 (System/database error):`<br>`- System displays a system error message.                                |
| Priority          | Medium, Should Have                                                                                          |
| Frequency of Use  | Occasionally, for system monitoring and troubleshooting                                                      |
| Business Rules    | - Only admins can view system logs.`<br>`- Logs are displayed in chronological order.                      |

| Field             | Description                                                                                                                                 |
| ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| UC ID and Name    | UC_41: View Logs by Date                                                                                                                    |
| Created By        | Vo Van Hai                                                                                                                                  |
| Date Created      | 19/11/2025                                                                                                                                  |
| Primary Actor     | Admin                                                                                                                                       |
| Secondary Actors  | None                                                                                                                                        |
| Trigger           | Admin filters and views activity logs by date via the logs interface.                                                                       |
| Description       | Allows the admin to filter and view system activity logs by date. The system displays logs for the selected date range.                     |
| Preconditions     | PRE-1: Admin is authenticated.`<br>`PRE-2: The system is operational.`<br>`PRE-3: Logs exist for the selected date range.               |
| Postconditions    | POST-1: Logs for the selected date range are displayed.`<br>`POST-2: If no logs, a message is shown.                                      |
| Normal Flow       | 1. Admin accesses logs page.`<br>`2. Selects date range filter.`<br>`3. System retrieves and displays logs for the selected date range. |
| Alternative Flows | 3.1 (No logs found for date range):`<br>`- System displays "No logs available for selected date."                                         |
| Exceptions        | 3.2 (System/database error):`<br>`- System displays a system error message.                                                               |
| Priority          | Medium, Should Have                                                                                                                         |
| Frequency of Use  | Occasionally, for system monitoring and troubleshooting                                                                                     |
| Business Rules    | - Only admins can filter and view logs by date.`<br>`- Logs are displayed in chronological order for the selected range.                  |

Future Extension Directions

Add a Price Field for Books

Each book will have an additional “price” field in its detailed information.

Support for displaying prices, promotional programs, or discounts.

Expand the Role System

Add an “Author” role alongside User and Admin.

Authors have the right to manage the books they have posted and view statistics for their own books.

Author Role Registration Process

Users who want to become authors must submit an application form with verification information.

The application will be reviewed and approved by an admin. Only after approval will the account be upgraded to the Author role.

Author Rights and Processes

Authors can create, edit, and delete (CRUD) the books they have posted.

When an author posts a new book or edits a book, the content will be in a “pending approval” state.

Admins will review and approve the book content before it is published on the system.

If rejected, the admin must select or enter a reason (e.g., copyright violation, inappropriate content, duplication, missing information, etc.) and notify the author for revision.

Approval History Management and Notifications

Maintain a history of approvals, reasons for rejection, time, and the person who performed the action.

The system sends real-time notifications to the author when a book is approved, rejected, or receives feedback from the admin.

Expand the Author Dashboard

Authors can view statistics about their books: number of views, purchases, ratings, and approval status.

* 
* [•••]()
* 
* Go to[ ] Page
