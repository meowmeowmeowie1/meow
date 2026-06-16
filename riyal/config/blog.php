<?php

return [
    /*
    |--------------------------------------------------------------------------
    | Locales
    |--------------------------------------------------------------------------
    | The locales the blog is published in. Each post belongs to exactly one
    | locale (per-post locale model) and is linked to its counterpart in the
    | other locale through a shared translation_group_id.
    */

    'default_locale' => env('BLOG_DEFAULT_LOCALE', 'ar'),

    'locales' => [
        'ar' => [
            'name' => 'العربية',
            'native' => 'العربية',
            'dir' => 'rtl',
            'html_lang' => 'ar',
        ],
        'en' => [
            'name' => 'English',
            'native' => 'English',
            'dir' => 'ltr',
            'html_lang' => 'en',
        ],
    ],

    // hreflang x-default points at this locale.
    'x_default_locale' => 'en',
];
