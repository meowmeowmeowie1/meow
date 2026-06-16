# ريال · Riyal — Bilingual (AR/EN) AdSense Finance Blog

A bilingual Arabic/English personal-finance blog for the Gulf market, built on
**Laravel 13 + Blade + Tailwind CSS v4**. It implements the build brief: full
RTL/LTR support, per-post locale with translation pairing, SEO (hreflang,
JSON-LD, sitemap), AdSense-ready ad slots, and a lightweight admin.

> This app lives in the `riyal/` subdirectory of the repository. The repository
> root contains an unrelated project; the blog was added here so nothing else
> was disturbed.

## Stack

- Laravel 13 (PHP 8.3+)
- Blade + Tailwind CSS v4 (via Vite) — RTL via logical utilities (`ms-`, `me-`, `ps-`, `pe-`, `text-start`)
- MySQL/MariaDB in production; **SQLite by default** for zero-config local dev
- `league/commonmark` for Markdown post bodies
- Hand-rolled Blade admin (auth via Laravel's session guard)

## Quick start

```bash
cd riyal
composer install
cp .env.example .env
php artisan key:generate

# SQLite (default): create the database file
touch database/database.sqlite

php artisan migrate --seed      # schema + 12 starter posts (6 AR / 6 EN)

npm install
npm run build                   # or: npm run dev

php artisan serve
```

Visit `http://localhost:8000` → redirects to `/ar`. Switch languages with the
header toggle.

**Admin:** `http://localhost:8000/admin` · seeded login
`editor@riyal.blog` / `password` (change immediately).

### Using MySQL instead

Set in `.env`:

```env
DB_CONNECTION=mysql
DB_HOST=127.0.0.1
DB_PORT=3306
DB_DATABASE=riyal
DB_USERNAME=root
DB_PASSWORD=
```

The schema is portable; `enum` columns map cleanly to MySQL.

## How the bilingual model works

- **Per-post locale.** Each `posts` row has a `locale` (`ar`/`en`). Routes are
  locale-prefixed: `/{locale}/p/{slug}`.
- **Translation pairs.** AR and EN versions of the same article share a
  `translation_group_id` (a UUID). This drives `hreflang` tags, the sitemap
  alternates, and the language switcher (switch goes to the counterpart if one
  exists, else the other-locale homepage). Link them in
  **Admin → Posts → Edit → Link translation**.
- `<html dir>` / `lang` are set from the route locale by the `SetLocale`
  middleware (`app/Http/Middleware/SetLocale.php`).

## AdSense

- The loader script is injected once in `<head>` when `adsense_client_id` is set.
- `<x-ad placement="in_article|mid_article|below_post|sidebar" />` renders a
  responsive unit, pulling slot IDs from the `settings` table — so ad codes
  change **without a redeploy**. (The prop is `placement`, not `slot`, because
  `$slot` is reserved by Blade components.)
- Placement: after the intro and around the midpoint of long posts, plus one
  below the article. **No ads on thin/legal pages.**
- `/ads.txt` is generated from settings (`ads_txt_line`, or auto-built from
  `adsense_client_id`).
- Configure everything in **Admin → Settings**. Apply to AdSense only after
  15–20 solid posts + About/Privacy/Terms are live.

## SEO

- `meta_title` / `meta_description` per post (fall back to title/excerpt).
- `hreflang` AR↔EN with `x-default`; Open Graph / Twitter cards.
- JSON-LD `Article` + `BreadcrumbList` on posts.
- `/sitemap.xml` (with `xhtml:link` alternates), `/robots.txt`.

## Admin (`/admin`)

CRUD for posts (Markdown editor, locale, category, comma-separated tags, cover,
SEO fields, featured, draft/publish, schedule via `published_at`), categories,
tags, and settings, plus the translation-linking action.

## Tests

```bash
php artisan test
```

`tests/Feature/BlogTest.php` covers locale routing/RTL, hreflang + JSON-LD,
draft visibility, search, the SEO files, `ads.txt` generation, admin auth,
post creation, and translation linking.

## Production notes

```bash
php artisan config:cache && php artisan route:cache && php artisan view:cache
npm run build
```

Serve behind Nginx + PHP-FPM (or Laravel Forge). Use a CDN for images and a
real mail driver to receive contact-form submissions (currently logged).

## Key paths

| Area | Path |
|------|------|
| Routes | `routes/web.php` |
| Models | `app/Models/{Post,Category,Tag,Setting,User}.php` |
| Public controllers | `app/Http/Controllers/` |
| Admin controllers | `app/Http/Controllers/Admin/` |
| Middleware | `app/Http/Middleware/{SetLocale,EnsureUserIsAdmin}.php` |
| Locale config | `config/blog.php` |
| Views | `resources/views/` (layout, partials, components, admin) |
| Ad component | `resources/views/components/ad.blade.php` |
| Translations | `lang/{ar,en}/messages.php` |
| Seed content | `database/seeders/BlogSeeder.php` |
