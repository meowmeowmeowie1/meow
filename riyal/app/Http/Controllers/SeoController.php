<?php

namespace App\Http\Controllers;

use App\Models\Category;
use App\Models\Post;
use App\Models\Setting;
use Illuminate\Http\Response;

class SeoController extends Controller
{
    /**
     * XML sitemap with hreflang alternates. Every post is listed once per
     * locale it exists in, and AR/EN counterparts cross-reference each other
     * via xhtml:link alternates so Search Console understands the pairing.
     */
    public function sitemap(): Response
    {
        $locales = array_keys(config('blog.locales'));
        $urls = [];

        // Static localized pages.
        foreach ($locales as $locale) {
            foreach (['home', 'pages.about', 'pages.contact', 'pages.privacy', 'pages.terms'] as $name) {
                $urls[] = [
                    'loc' => route($name, ['locale' => $locale]),
                    'lastmod' => null,
                    'alternates' => collect($locales)->mapWithKeys(fn ($l) => [
                        $l => route($name, ['locale' => $l]),
                    ])->all(),
                ];
            }
        }

        // Posts, with counterpart alternates.
        Post::query()->published()->with('category')->chunk(200, function ($posts) use (&$urls) {
            foreach ($posts as $post) {
                $alternates = [$post->locale => $post->url()];
                if ($counterpart = $post->counterpart()) {
                    $alternates[$counterpart->locale] = $counterpart->url();
                }

                $urls[] = [
                    'loc' => $post->url(),
                    'lastmod' => optional($post->updated_at)->toAtomString(),
                    'alternates' => $alternates,
                ];
            }
        });

        // Category indexes.
        Category::query()->each(function (Category $category) use (&$urls) {
            $urls[] = [
                'loc' => $category->url(),
                'lastmod' => null,
                'alternates' => [],
            ];
        });

        $xml = view('seo.sitemap', [
            'urls' => $urls,
            'xDefault' => config('blog.x_default_locale'),
        ])->render();

        return response($xml, 200, ['Content-Type' => 'application/xml']);
    }

    public function robots(): Response
    {
        $lines = [
            'User-agent: *',
            'Disallow: /admin',
            'Allow: /',
            'Sitemap: '.url('/sitemap.xml'),
        ];

        return response(implode("\n", $lines)."\n", 200, ['Content-Type' => 'text/plain']);
    }

    /**
     * ads.txt is required by AdSense. The publisher line is assembled from the
     * settings table so it can change without a redeploy.
     */
    public function adsTxt(): Response
    {
        $line = Setting::get('ads_txt_line');

        if (! $line) {
            $publisher = Setting::get('adsense_client_id'); // e.g. ca-pub-0000000000000000
            if ($publisher) {
                $pub = str_replace('ca-pub-', 'pub-', $publisher);
                $line = "google.com, {$pub}, DIRECT, f08c47fec0942fa0";
            }
        }

        return response(($line ?: '# Add your AdSense publisher line in Admin → Settings')."\n", 200, [
            'Content-Type' => 'text/plain',
        ]);
    }
}
