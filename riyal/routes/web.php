<?php

use App\Http\Controllers\Admin\AuthController;
use App\Http\Controllers\Admin\DashboardController;
use App\Http\Controllers\Admin\PostController as AdminPostController;
use App\Http\Controllers\Admin\TaxonomyController;
use App\Http\Controllers\Admin\SettingController;
use App\Http\Controllers\CategoryController;
use App\Http\Controllers\HomeController;
use App\Http\Controllers\PageController;
use App\Http\Controllers\PostController;
use App\Http\Controllers\SearchController;
use App\Http\Controllers\SeoController;
use App\Http\Controllers\TagController;
use Illuminate\Support\Facades\Route;

/*
|--------------------------------------------------------------------------
| SEO / AdSense files (no locale prefix — served at the site root)
|--------------------------------------------------------------------------
*/
Route::get('/sitemap.xml', [SeoController::class, 'sitemap'])->name('sitemap');
Route::get('/robots.txt', [SeoController::class, 'robots'])->name('robots');
Route::get('/ads.txt', [SeoController::class, 'adsTxt'])->name('ads');

/*
|--------------------------------------------------------------------------
| Admin
|--------------------------------------------------------------------------
*/
Route::prefix('admin')->name('admin.')->group(function () {
    Route::get('login', [AuthController::class, 'show'])->name('login');
    Route::post('login', [AuthController::class, 'login'])->name('login.attempt');
    Route::post('logout', [AuthController::class, 'logout'])->name('logout');

    Route::middleware('admin')->group(function () {
        Route::get('/', [DashboardController::class, 'index'])->name('dashboard');

        Route::get('posts', [AdminPostController::class, 'index'])->name('posts.index');
        Route::get('posts/create', [AdminPostController::class, 'create'])->name('posts.create');
        Route::post('posts', [AdminPostController::class, 'store'])->name('posts.store');
        Route::get('posts/{post}/edit', [AdminPostController::class, 'edit'])->name('posts.edit');
        Route::put('posts/{post}', [AdminPostController::class, 'update'])->name('posts.update');
        Route::delete('posts/{post}', [AdminPostController::class, 'destroy'])->name('posts.destroy');
        Route::post('posts/{post}/link', [AdminPostController::class, 'link'])->name('posts.link');

        Route::get('categories', [TaxonomyController::class, 'categories'])->name('categories');
        Route::post('categories', [TaxonomyController::class, 'storeCategory'])->name('categories.store');
        Route::delete('categories/{category}', [TaxonomyController::class, 'destroyCategory'])->name('categories.destroy');

        Route::get('tags', [TaxonomyController::class, 'tags'])->name('tags');
        Route::post('tags', [TaxonomyController::class, 'storeTag'])->name('tags.store');
        Route::delete('tags/{tag}', [TaxonomyController::class, 'destroyTag'])->name('tags.destroy');

        Route::get('settings', [SettingController::class, 'edit'])->name('settings');
        Route::put('settings', [SettingController::class, 'update'])->name('settings.update');
    });
});

/*
|--------------------------------------------------------------------------
| Public, locale-prefixed site
|--------------------------------------------------------------------------
*/
Route::prefix('{locale}')
    ->where(['locale' => 'ar|en'])
    ->middleware('setlocale')
    ->group(function () {
        Route::get('/', [HomeController::class, 'index'])->name('home');
        Route::get('/p/{slug}', [PostController::class, 'show'])->name('posts.show');
        Route::get('/category/{slug}', [CategoryController::class, 'show'])->name('categories.show');
        Route::get('/tag/{slug}', [TagController::class, 'show'])->name('tags.show');
        Route::get('/search', [SearchController::class, 'index'])->name('search');

        Route::get('/about', [PageController::class, 'about'])->name('pages.about');
        Route::get('/contact', [PageController::class, 'contact'])->name('pages.contact');
        Route::post('/contact', [PageController::class, 'submitContact'])->name('pages.contact.submit');
        Route::get('/privacy', [PageController::class, 'privacy'])->name('pages.privacy');
        Route::get('/terms', [PageController::class, 'terms'])->name('pages.terms');
    });

// Root redirects to the default locale's homepage.
Route::get('/', fn () => redirect('/'.config('blog.default_locale')));
