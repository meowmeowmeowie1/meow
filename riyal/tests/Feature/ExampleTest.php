<?php

namespace Tests\Feature;

// use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class ExampleTest extends TestCase
{
    /**
     * A basic test example.
     */
    public function test_the_application_returns_a_successful_response(): void
    {
        // The root path redirects to the default locale's homepage.
        $response = $this->get('/');

        $response->assertRedirect('/'.config('blog.default_locale'));
    }
}
